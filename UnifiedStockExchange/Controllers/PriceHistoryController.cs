using Microsoft.AspNetCore.Mvc;
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
        /// Retrieves aggregated data based on <paramref name="body.CandleInterval"/>. Returned data contains
        /// <paramref name="candleSamples"/> samples of data or less in case there is not enough data.
        /// The implementation should always cache latest data to allow components to access it fast multiple times.
        /// </summary>
        [HttpPost("[action]")]
        public ActionResult<IEnumerable<PriceCandle>> GetHistoryData(PriceHistoryRequest body)
        {
            if (body.FromDate.Year < 1900 || body.EndDate.Year < 1900)
                return BadRequest("Please specify FromDate and EndDate after year 1900.");

            if (body.CandleInterval < PersistenceInterval)
                return BadRequest("Cannot request data more granular than " + PersistenceInterval);

            var priceFilter = _persistenceService.SelectPriceData(body.ExchangeName, body.TradingPair.ToTradingPair());
            var historyData = priceFilter.Where(it => it.Date >= body.FromDate && it.Date < body.EndDate).OrderBy(it => it.Date).ExecuteSelect();
            List<PriceCandle> result = new List<PriceCandle>();

            PriceCandle? currentSample = null;
            for (int index = 0; index < historyData.Count; index++)
            {
                if (currentSample == null)
                {
                    // Initialize first PriceCandle and add it to result.
                    currentSample = historyData[index].Clone();
                    currentSample.Date = currentSample.Date.TruncateByInterval(body.CandleInterval);
                    currentSample.Interval = body.CandleInterval;
                }
                else if (currentSample.Date != historyData[index].Date.TruncateByInterval(body.CandleInterval))
                {
                    currentSample.Close = historyData[index - 1].Close;
                    result.Add(currentSample);

                    // Fill missing samples with previous sample close value.
                    while (currentSample.Date.AddMinutes((int)body.CandleInterval) != historyData[index].Date.TruncateByInterval(body.CandleInterval))
                    {
                        decimal previousClose = currentSample.Close;
                        currentSample = new PriceCandle { Date = currentSample.Date.AddMinutes((int)body.CandleInterval) };
                        currentSample.Open = currentSample.High = currentSample.Low = currentSample.Close = previousClose;
                    }

                    // Create another PriceCandle and add it to result.
                    currentSample = historyData[index].Clone();
                    currentSample.Date = currentSample.Date.TruncateByInterval(body.CandleInterval);
                    currentSample.Interval = body.CandleInterval;
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

            return Ok(result);
        }
    }
}
