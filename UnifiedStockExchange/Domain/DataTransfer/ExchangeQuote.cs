namespace UnifiedStockExchange.Domain.DataTransfer
{
    public class ExchangeQuote
    {
        public int Id { get; set; }
        public string ExchangeName { get; set; } = null!;
        public ValueTuple<string, string> TradingPair { get; set; }
    }
}
