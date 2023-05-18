using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnifiedStockExchange.Sdk.CSharp.Model;

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

        /// <summary>
        /// Adjust the specified <paramref name="date"/> so that it is a multiple of <paramref name="interval"/>.
        /// </summary>
        internal static DateTime TruncateByInterval(this DateTime date, SampleInterval interval)
        {
            int dayMinutes = (int)date.TimeOfDay.TotalMinutes;
            date = date.Date.AddMinutes(dayMinutes / (int)interval * (int)interval);
            return date;
        }
    }
}
