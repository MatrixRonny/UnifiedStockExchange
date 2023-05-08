using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnifiedStockExchange.CSharp.Tests
{
    internal static class TestUtility
    {
        internal static AppSettings GetAppSettings()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            return config.Get<AppSettings>();
        }

        internal static ValueTuple<string, string> ToTradingPair(this string quote)
        {
            string[] split = quote.Split('-');
            return (split[1], split[0]);
        }
    }
}
