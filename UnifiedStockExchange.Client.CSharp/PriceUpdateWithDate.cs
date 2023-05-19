using System;
using System.Collections.Generic;
using System.Text;

namespace UnifiedStockExchange.Client.CSharp
{
    public class PriceUpdateWithDate
    {
        public DateTime Time { get; set; }

        public decimal Price { get; set; }

        public decimal Amount { get; set; }

        public string TradingPair { get; set; } = null;
    }
}
