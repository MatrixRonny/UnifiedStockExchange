namespace UnifiedStockExchange.Domain.Entities
{
    public class ExchangeQuote
    {
        public int Id { get; set; }
        public string ExchangeName { get; set; } = null!;
        public string TradingPair { get; set; } = null!;
    }
}
