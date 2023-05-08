using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ServiceStack.OrmLite;
using System.Data;
using UnifiedStockExchange.DataAccess;
using UnifiedStockExchange.Domain.Entities;

namespace UnifiedStockExchange.Services
{
    /// <summary>
    /// Stores PriceCandle values to different tables identified by exchangeName and tradingPair.
    /// Multiple price updates are aggregated to a single PriceCandle once per minute.
    /// </summary>
    public class PricePersistenceService
    {
        //TODO: Implement tool to join DB files together or create DB with new data only.

        readonly static TimeSpan CacheTimeout = TimeSpan.FromMinutes(10);
        static DateTime _lastCleanup;

        private readonly OrmLiteConnectionFactory _connectionFactory;
        Dictionary<string, DateTime> _lastWrite = new();
        Dictionary<string, PriceCandle> _priceData = new();
        Dictionary<string, TableDataAccess<PriceCandle>> _tableAccess = new();
        public PricePersistenceService(OrmLiteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void RecordPrice(string exchangeName, ValueTuple<string, string> tradingPair, DateTime time, decimal price, decimal amount)
        {
            string exchangeQuote = $"{exchangeName}|{tradingPair.Item1}-{tradingPair.Item2}";

            DateTime lastWrite;
            lock (_lastWrite)
            {
                DateTime dateTimeNow = DateTime.UtcNow;

                if (_lastCleanup != DateTime.UtcNow.Date)
                {
                    // Time to clean up old entries from _tableAccess cache.

                    _lastCleanup = DateTime.UtcNow.Date;

                    IEnumerable<string> expiredKeys = _lastWrite
                        .Where(it => (dateTimeNow - it.Value) > CacheTimeout)
                        .Select(it => it.Key);
                    foreach (string key in expiredKeys)
                    {
                        _lastWrite.Remove(key);
                        _priceData.Remove(key);
                        _tableAccess.Remove(key);
                    }
                }

                if (!_lastWrite.ContainsKey(exchangeQuote))
                {
                    // Create missing exchangeQuote cache and data access.

                    lastWrite = new DateTime();

                    _lastWrite[exchangeQuote] = DateTime.UtcNow;
                    _priceData[exchangeQuote] = new PriceCandle
                    {
                        Date = TruncateDateToMinute(dateTimeNow),
                        Open = price,
                        High = price,
                        Low = price,
                        Close = price,
                        Volume = 0  // Will be adjusted when updating PriceCandle
                    };
                    _tableAccess[exchangeQuote] = new TableDataAccess<PriceCandle>(_connectionFactory, "PriceData_" + exchangeQuote);
                }
                else
                {
                    lastWrite = _lastWrite[exchangeQuote];
                }
            }

            PriceCandle priceCandle = _priceData[exchangeQuote];
            lock (priceCandle)
            {
                if ((time.Date - lastWrite).Minutes < 1)
                {
                    // Update existing PriceCandle with current price information.

                    if (priceCandle.Low > price) priceCandle.Low = price;
                    if (priceCandle.High < price) priceCandle.High = price;

                    priceCandle.Volume += amount;
                }
                else
                {
                    // Store existing PriceCandle and create new one.

                    priceCandle.Close = price;

                    TableDataAccess<PriceCandle> dataAccess = _tableAccess[exchangeQuote];
                    lock (dataAccess)
                    {
                        dataAccess.Insert(_priceData[exchangeQuote]);
                    }

                    priceCandle.Date = TruncateDateToMinute(time);
                    priceCandle.Open = priceCandle.High = priceCandle.Low = price;
                    priceCandle.Close = 0;
                    priceCandle.Volume = amount;
                }
            }
        }

        private static DateTime TruncateDateToMinute(DateTime dateTimeNow)
        {
            return new DateTime(dateTimeNow.Year, dateTimeNow.Month, dateTimeNow.Day, dateTimeNow.Hour, dateTimeNow.Minute, 0);
        }
    }
}
