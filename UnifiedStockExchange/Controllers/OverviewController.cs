using Microsoft.AspNetCore.Mvc;
using UnifiedStockExchange.Services;

namespace UnifiedStockExchange.Controllers
{
    public class OverviewController : ApiControllerBase
    {
        private readonly PriceExchangeService _exchangeService;

        public OverviewController(PriceExchangeService exchangeService)
        {
            _exchangeService = exchangeService;
        }

        [HttpPost("[action]/{exchangeName}")]
        public IEnumerable<string> GetAvailableCurrencyPairs(string exchangeName)
        {
            return _exchangeService.ActiveExchangeQuotes[exchangeName];
        }

        [HttpPost("[action]")]
        public IEnumerable<string> GetAvailableExchanges()
        {
            return _exchangeService.ActiveExchangeQuotes.Keys;
        }
    }
}