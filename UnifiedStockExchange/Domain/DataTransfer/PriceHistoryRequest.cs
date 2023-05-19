using UnifiedStockExchange.Domain.Enums;

namespace UnifiedStockExchange.Domain.DataTransfer
{
    public class PriceHistoryRequest
    {
        public string ExchangeName { get; set; } = null!;
        public string TradingPair { get; set; } = null!;
        public DateTime? FromDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int CandleSamples { get; set; }
        public SampleInterval CandleInterval { get; set; }
    }
}
