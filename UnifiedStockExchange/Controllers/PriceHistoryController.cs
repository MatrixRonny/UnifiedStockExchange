using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite.DataAccess;
using System.Data;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.DataTransfer;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Domain.Enums;
using UnifiedStockExchange.Services;
using UnifiedStockExchange.Utility;
using static UnifiedStockExchange.Domain.Constants.StockConstants;

namespace UnifiedStockExchange.Controllers
{
    public class PriceHistoryController : ApiControllerBase
    {
        private readonly PricePersistenceService _persistenceService;

        public PriceHistoryController(PricePersistenceService persistenceService)
        {
            _persistenceService = persistenceService;
        }

        /// <summary>
        /// Retrieves aggregated data based on <paramref name="request.CandleInterval"/>. Returned data contains
        /// <paramref name="candleSamples"/> samples of data or less in case there is not enough data.
        /// The implementation should always cache latest data to allow components to access it fast multiple times.
        /// </summary>
        [HttpPost("[action]")]
        public ActionResult<IEnumerable<PriceCandle>> GetHistoryData(PriceHistoryRequest request)
        {
            if (request.StartDate.Year < 1900 || request.EndDate.Year < 1900)
                return BadRequest("Please specify FromDate and EndDate after year 1900.");

            if (request.CandleInterval < PersistenceInterval)
                return BadRequest("Cannot request data more granular than " + PersistenceInterval);

            var priceFilter = _persistenceService.SelectPriceData(request.ExchangeName, request.TradingPair.ToTradingPair());
            var historyData = priceFilter.Where(it => it.Date >= request.StartDate && it.Date < request.EndDate).OrderBy(it => it.Date).ExecuteSelect();
            List<PriceCandle> result = new List<PriceCandle>();

            priceFilter = _persistenceService.SelectPriceData(request.ExchangeName, request.TradingPair.ToTradingPair());
            FillMissingSamplesStart(request, priceFilter, historyData, result);

            PriceCandle? currentSample = null;
            for (int index = 0; index < historyData.Count; index++)
            {
                if (currentSample == null)
                {
                    // Initialize first PriceCandle and add it to result.
                    currentSample = historyData[index].Clone();
                    currentSample.Date = currentSample.Date.TruncateByInterval(request.CandleInterval);
                    currentSample.Interval = request.CandleInterval;
                }
                else if (currentSample.Date != historyData[index].Date.TruncateByInterval(request.CandleInterval))
                {
                    currentSample.Close = historyData[index - 1].Close;
                    result.Add(currentSample);

                    // Fill missing samples with previous sample close value.
                    while (currentSample.Date.AddMinutes((int)request.CandleInterval) != historyData[index].Date.TruncateByInterval(request.CandleInterval))
                    {
                        decimal previousClose = currentSample.Close;
                        currentSample = new PriceCandle { Date = currentSample.Date.AddMinutes((int)request.CandleInterval) };
                        currentSample.Open = currentSample.High = currentSample.Low = currentSample.Close = previousClose;
                    }

                    // Create another PriceCandle and add it to result.
                    currentSample = historyData[index].Clone();
                    currentSample.Date = currentSample.Date.TruncateByInterval(request.CandleInterval);
                    currentSample.Interval = request.CandleInterval;
                }
                else
                {
                    // Update current PriceCandle data with historyData price at current index;

                    PriceCandle newSample = historyData[index];

                    if (currentSample.Low > historyData[index].Low)
                    {
                        currentSample.Low = historyData[index].Low;
                    }

                    if (currentSample.High < historyData[index].High)
                    {
                        currentSample.High = historyData[index].High;
                    }

                    currentSample.Volume += historyData[index].Volume;
                }
            }

            // After the loop, the currentSample needs to be added to result.
            if (currentSample != null)
            {
                currentSample.Close = historyData.Last().Close;
                result.Add(currentSample);
            }

            priceFilter = _persistenceService.SelectPriceData(request.ExchangeName, request.TradingPair.ToTradingPair());
            FillMissingSamplesEnd(request, priceFilter, result, currentSample);

            return Ok(result);
        }

        private static void FillMissingSamplesStart(PriceHistoryRequest request, ISelectFilter<PriceCandle> priceFilter, IList<PriceCandle> historyData, List<PriceCandle> result)
        {
            if (!historyData.IsEmpty())
            {
                DateTime startDate = historyData.First().Date.TruncateByInterval(request.CandleInterval);
                if (startDate != request.StartDate.TruncateByInterval(request.CandleInterval))
                {
                    decimal lastPrice = priceFilter.Where(it => it.Date < request.EndDate).OrderByDescending(it => it.Date).Take(1).ExecuteSelect()[0].Close;
                    for (DateTime currentDate = request.StartDate.TruncateByInterval(request.CandleInterval); currentDate < startDate; currentDate = currentDate.AddMinutes((int)request.CandleInterval))
                    {
                        PriceCandle priceCandle = new PriceCandle
                        {
                            Date = currentDate,
                            Interval = request.CandleInterval,
                            Volume = 0
                        };
                        priceCandle.Open = priceCandle.High = priceCandle.Low = priceCandle.Close = lastPrice;
                        result.Add(priceCandle);
                    }
                }
            }
        }

        private static void FillMissingSamplesEnd(PriceHistoryRequest request, ISelectFilter<PriceCandle> priceFilter, List<PriceCandle> result, PriceCandle? currentSample)
        {
            bool isDiscreteEnd = request.EndDate.TruncateByInterval(request.CandleInterval) == request.EndDate;
            if ((isDiscreteEnd && currentSample?.Date != request.EndDate.AddMinutes(-(int)request.CandleInterval)) ||
                    (!isDiscreteEnd && currentSample?.Date != request.EndDate.TruncateByInterval(request.CandleInterval)))
            {
                DateTime startDate;
                decimal lastPrice;
                if (currentSample == null)
                {
                    startDate = request.StartDate.TruncateByInterval(request.CandleInterval);
                    lastPrice = priceFilter.Where(it => it.Date < request.EndDate).OrderByDescending(it => it.Date).Take(1).ExecuteSelect()[0].Close;
                }
                else
                {
                    startDate = currentSample.Date.TruncateByInterval(request.CandleInterval).AddMinutes((int)request.CandleInterval);
                    lastPrice = currentSample.Close;
                }

                for (DateTime currentDate = startDate; currentDate < request.EndDate; currentDate = currentDate.AddMinutes((int)request.CandleInterval))
                {
                    PriceCandle priceCandle = new PriceCandle
                    {
                        Date = currentDate,
                        Interval = request.CandleInterval,
                        Volume = 0
                    };
                    priceCandle.Open = priceCandle.High = priceCandle.Low = priceCandle.Close = lastPrice;
                    result.Add(priceCandle);
                }
            }
        }
    }
}
