using Microsoft.AspNetCore.Mvc;

namespace UnifiedStockExchange.Controllers
{
    public class OverviewController : ApiControllerBase
    {
        [HttpPost("[action]/{exchange}")]
        public IEnumerable<string> GetAvailableCurrencyPairs(string exchange)
        {
            return new string[]
            {
                "BTC-USDT",
                "ETH-USDT"
            };
        }

        [HttpPost("[action]")]
        public IEnumerable<string> GetAvailableExchanges()
        {
            return new string[]
            {
                "CoinMarketCap"
            };
        }
    }
}