using UnifiedStockExchange.Domain.Enums;

namespace UnifiedStockExchange.Domain.Entities
{
    public class PriceCandle
    {
        public DateTime Date { get; set; }
        public SampleInterval Interval { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }
}
