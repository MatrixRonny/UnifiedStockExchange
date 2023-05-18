using Microsoft.AspNetCore.Mvc;
using System.Data;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Domain.Enums;
using UnifiedStockExchange.Exceptions;
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
        /// Retrieves aggregated data based on <paramref name="candleInterval"/>. Returned data contains
        /// <paramref name="candleSamples"/> samples of data or less in case there is not enough data.
        /// The implementation should always cache latest data to allow components to access it fast multiple times.
        /// </summary>
        [HttpPost("[action]")]
        public IEnumerable<PriceCandle> GetHistoryDataFrom(string exchangeName, string tradingPair, DateTime fromDate, int candleSamples, SampleInterval candleInterval)
        {
            if (candleInterval < PersistenceInterval)
                throw new StockDataException("Cannot request data more granular than " + PersistenceInterval);

            fromDate = fromDate.TruncateByInterval(candleInterval);
            DateTime untilDate = fromDate.AddMinutes((int)candleInterval * candleSamples);

            var priceFilter = _persistenceService.SelectPriceData(exchangeName, tradingPair.ToTradingPair());
            var historyData = priceFilter.Where(it => it.Date >= fromDate && it.Date < untilDate).ExecuteSelect();
            List<PriceCandle> result = new List<PriceCandle>();

            PriceCandle? currentSample = null;
            for (int index = 0; index < historyData.Count; index++)
            {
                if (currentSample == null)
                {
                    // Initialize first PriceCandle and add it to result.
                    currentSample = historyData[index].Clone();
                    currentSample.Date = currentSample.Date.TruncateByInterval(candleInterval);
                    currentSample.Interval = candleInterval;
                    result.Add(currentSample);
                }
                else if (currentSample.Date != historyData[index].Date.TruncateByInterval(candleInterval))
                {
                    // Fill missing samples with previous sample average.
                    while (currentSample.Date.AddMinutes((int)candleInterval) != historyData[index].Date.TruncateByInterval(candleInterval))
                    {
                        decimal average = (currentSample.Open + currentSample.Close) / 2;
                        currentSample = new PriceCandle { Date = currentSample.Date.AddMinutes((int)candleInterval) };
                        currentSample.Open = currentSample.High = currentSample.Low = currentSample.Close = average;
                    }

                    // Create another PriceCandle and add it to result.
                    currentSample = historyData[index].Clone();
                    currentSample.Date = currentSample.Date.TruncateByInterval(candleInterval);
                    currentSample.Interval = candleInterval;
                    result.Add(currentSample);
                }
                else
                {
                    // Update current PriceCandle data with historyData price at current index;

                    PriceCandle newSample = historyData[index];
                    currentSample.Close = newSample.Close;

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

            return result;
        }

        /// <summary>
        /// Based on <see cref="GetHistoryDataFrom(DateTime, int, SampleInterval)"/>, but the <paramref name="candleSamples"/>
        /// are retrieved before <paramref name="endDate"/>.
        /// </summary>
        [HttpPost("[action]")]
        public IEnumerable<PriceCandle> GetHistoryDataUntil(string exchange, string tradingPair, DateTime endDate, int candleSamples, SampleInterval candleInterval)
        {
            DateTime startTime = endDate.AddMinutes(-(int)candleInterval * (candleSamples - 1));
            return GetHistoryDataFrom(exchange, tradingPair, startTime, candleSamples, candleInterval);
        }
    }
}
