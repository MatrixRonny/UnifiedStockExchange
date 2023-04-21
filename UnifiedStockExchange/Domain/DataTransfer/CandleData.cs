using UnifiedStockExchange.Domain.Enums;

namespace UnifiedStockExchange.Domain.DataTransfer
{
    public class CandleData
    {
        public DateTime Date { get; set; }
        public SampleInterval Interval { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }

        public CandleData Clone()
        {
            return new CandleData
            {
                Date = Date,
                Interval = Interval,
                Open = Open,
                High = High,
                Low = Low,
                Close = Close,
                Volume = Volume
            };
        }
    }
}
