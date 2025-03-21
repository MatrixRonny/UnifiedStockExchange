﻿using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.DataAccess;
using System.Data;
using System.Data.SQLite;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Domain.Enums;
using UnifiedStockExchange.Utility;
using static UnifiedStockExchange.Domain.Constants.StockConstants;

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
            //TODO: Change this method to return an object that can be used to record price for specific exchange and tradingPair.

            string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);

            DateTime lastWrite;
            lock (_lastWrite)
            {
                DateTime dateTimeNow = DateTime.UtcNow;

                if (_lastCleanup + CacheTimeout < dateTimeNow)
                {
                    // Clean up old entries from _tableAccess cache.

                    _lastCleanup = dateTimeNow;

                    IEnumerable<string> expiredKeys = _lastWrite
                        .Where(it => (dateTimeNow - it.Value) > CacheTimeout)
                        .Select(it => it.Key);
                    foreach (string key in expiredKeys)
                    {
                        _lastWrite.Remove(key);
                        //_priceData.Remove(key);
                        _tableAccess.Remove(key);
                    }
                }

                if (!_lastWrite.ContainsKey(exchangeQuote))
                {
                    // Create missing exchangeQuote cache and data access.

                    time = TruncateDateToMinute(time);
                    TableDataAccess<PriceCandle> dataAccess = new TableDataAccess<PriceCandle>(_connectionFactory, "PriceData_" + exchangeQuote);
                    dataAccess.CreateTable();

                    // Prevent client to overwrite older data.
                    PriceCandle? lastPrice = dataAccess.CreateSelectFilter().OrderByDescending(it => it.Date).Take(1).ExecuteSelect().SingleOrDefault();
                    if (lastPrice != null && lastPrice.Date > time)
                        throw new ApplicationException("Could not record price earlier than last recorded price.");

                    _lastWrite[exchangeQuote] = dateTimeNow;
                    _priceData[exchangeQuote] = new PriceCandle
                    {
                        Date = time,
                        Interval = PersistenceInterval,
                        Open = price,
                        High = price,
                        Low = price,
                        Close = price,
                        Volume = 0  // Will be adjusted when updating PriceCandle
                    };

                    _tableAccess[exchangeQuote] = dataAccess;

                    try
                    {
                        dataAccess.Insert(_priceData[exchangeQuote]);
                    }
                    catch (SQLiteException e) when (e.ErrorCode == 19)
                    {
                        //EMPTY: The current sample may have been flushed due to WebSocket failure. On reconnect, the price has already
                        //  been recorded for the current date, considering that price is recorded once per PersistenceInterval.
                    }
                }
                else
                {
                    lastWrite = _lastWrite[exchangeQuote];
                }
            }

            PriceCandle priceCandle = _priceData[exchangeQuote];
            lock (priceCandle)
            {
                if ((time - priceCandle.Date).Minutes < (int)PersistenceInterval)
                {
                    // Update existing PriceCandle with current price information.

                    if (priceCandle.Low > price) priceCandle.Low = price;
                    if (priceCandle.High < price) priceCandle.High = price;

                    priceCandle.Close = price;
                    priceCandle.Volume += amount;

                    TableDataAccess<PriceCandle> dataAccess = _tableAccess[exchangeQuote];
                    dataAccess.CreateUpdateFilter().Where(it => it.Date == priceCandle.Date).ExecuteUpdate(priceCandle);
                }
                else
                {
                    // Create and insert new PriceCandle.

                    priceCandle.Date = TruncateDateToMinute(time);
                    priceCandle.Open = priceCandle.High = priceCandle.Low = priceCandle.Close = price;
                    priceCandle.Volume = amount;

                    try
                    {
                        TableDataAccess<PriceCandle> dataAccess = _tableAccess[exchangeQuote];
                        dataAccess.Insert(priceCandle);
                    }
                    catch (SQLiteException e) when (e.ErrorCode == 19)
                    {
                        //EMPTY: The current sample may have been flushed due to WebSocket failure. On reconnect, the price has already
                        //  been recorded for the current date, considering that price is recorded once per PersistenceInterval.
                    }
                }
            }
        }
        
        public void FlushAndRemoveFromCache(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
            lock(_lastWrite)
            {
                if(!_lastWrite.ContainsKey(exchangeQuote))
                    return;
            }

            PriceCandle priceCandle = _priceData[exchangeQuote];
            lock (priceCandle)
            {
                TableDataAccess<PriceCandle> dataAccess = _tableAccess[exchangeQuote];
                lock (dataAccess)
                {
                    dataAccess.CreateUpdateFilter().Where(it => it.Date == priceCandle.Date).ExecuteUpdate(_priceData[exchangeQuote]);
                }
            }

            lock (_lastWrite)
            {
                _lastWrite.Remove(exchangeQuote);
                _priceData.Remove(exchangeQuote);
                _tableAccess.Remove(exchangeQuote);
            }

        }

        public ISelectFilter<PriceCandle> SelectPriceData(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
            TableDataAccess<PriceCandle> priceData = new TableDataAccess<PriceCandle>(_connectionFactory, "PriceData_" + exchangeQuote);
            return priceData.CreateSelectFilter();
        }

        private static DateTime TruncateDateToMinute(DateTime dateTimeNow)
        {
            return new DateTime(dateTimeNow.Year, dateTimeNow.Month, dateTimeNow.Day, dateTimeNow.Hour, dateTimeNow.Minute, 0);
        }
    }
}
