using Microsoft.AspNetCore.Mvc;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Domain.Enums;

namespace UnifiedStockExchange.Controllers
{
    public class PriceHistoryController : ApiControllerBase
    {
        /// <summary>
        /// Retrieves aggregated data based on <paramref name="candleInterval"/>. Returned data contains
        /// <paramref name="candleSamples"/> samples of data or less in case there is not enough data.
        /// The implementation should always cache latest data to allow components to access it fast multiple times.
        /// </summary>
        [HttpPost("[action]")]
        public IEnumerable<PriceCandle> GetHistoryDataFrom(string exchange, ValueTuple<string> tradingPair, DateTime fromDate, int candleSamples, SampleInterval candleInterval)
        {
            throw new NotImplementedException("Retrieve price history from DB.");
        }

        /// <summary>
        /// Based on <see cref="GetHistoryDataFrom(DateTime, int, SampleInterval)"/>, but the <paramref name="candleSamples"/>
        /// are retrieved before <paramref name="endDate"/>.
        /// </summary>
        [HttpPost("[action]")]
        public IEnumerable<PriceCandle> GetHistoryDataUntil(string exchange, ValueTuple<string> tradingPair, DateTime endDate, int candleSamples, SampleInterval candleInterval)
        {
            throw new NotImplementedException("Retrieve price history from DB.");
        }
    }
}
