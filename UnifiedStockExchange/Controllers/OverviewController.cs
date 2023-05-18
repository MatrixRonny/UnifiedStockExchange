using Microsoft.AspNetCore.Mvc;

namespace UnifiedStockExchange.Controllers
{
    public class OverviewController : ApiControllerBase
    {
        [HttpPost("[action]/{exchange}")]
        public IEnumerable<string> GetAvailableCurrencyPairs(string exchange)
        {
            return new ValueTuple<string,string>[]
            {
                ("USDT","BTC"),
                ("USDT","ETH")
            }.Select(it => $"{it.Item2}-{it.Item1}");
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