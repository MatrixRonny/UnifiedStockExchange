namespace UnifiedStockExchange.Client.CSharp
{
    public class PriceUpdateWithTimestamp
    {
        public long Time { get; set; }

        public decimal Price { get; set; }

        public decimal Amount { get; set; }

        public string TradingPair { get; set; } = null;
    }
}