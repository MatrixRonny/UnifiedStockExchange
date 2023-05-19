using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnifiedStockExchange.CSharp.Tests
{
    internal class AppSettings
    {
        public string UnifiedStockExchangeUrl { get; set; } = null!;
        public string ExchangeName { get; set; } = null!;
        public string PairName { get; set; } = null!;
    }
}
