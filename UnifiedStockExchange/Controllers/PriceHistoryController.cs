using UnifiedStockExchange.Domain.DataTransfer;
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
        public IEnumerable<CandleData> GetHistoryDataFrom(string exchange, string quote, DateTime fromDate, int candleSamples, SampleInterval candleInterval)
        {
            throw new NotImplementedException("Retrieve price history from DB.");
        }

        /// <summary>
        /// Based on <see cref="GetHistoryDataFrom(DateTime, int, SampleInterval)"/>, but the <paramref name="candleSamples"/>
        /// are retrieved before <paramref name="endDate"/>.
        /// </summary>
        public IEnumerable<CandleData> GetHistoryDataUntil(string exchange, string quote, DateTime endDate, int candleSamples, SampleInterval candleInterval)
        {
            throw new NotImplementedException("Retrieve price history from DB.");
        }
    }
}
