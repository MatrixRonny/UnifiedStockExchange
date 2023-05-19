namespace UnifiedStockExchange.Domain.DataTransfer
{
    public class PriceUpdateData
    {
        public string TradingPair { get; set; } = null!;

        public long Time { get; set; }

        public decimal Price { get; set; }

        public decimal Amount { get; set; }
    }
}