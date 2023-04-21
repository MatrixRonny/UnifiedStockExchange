using Microsoft.AspNetCore.Mvc;

namespace UnifiedStockExchange.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OverviewController : ControllerBase
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