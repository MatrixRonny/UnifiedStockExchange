using Microsoft.AspNetCore.Mvc;

namespace UnifiedStockExchange.Controllers
{
    public class OverviewController : ApiControllerBase
    {
        [HttpPost("[action]/{exchange}")]
        public IEnumerable<ValueTuple<string,string>> GetAvailableCurrencyPairs(string exchange)
        {
            return new ValueTuple<string,string>[]
            {
                ("BTC","USDT"),
                ("ETH","USDT")
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