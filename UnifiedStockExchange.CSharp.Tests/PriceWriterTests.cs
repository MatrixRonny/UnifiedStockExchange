using System.Text.Json.Nodes;
using UnifiedStockExchange.Sdk.CSharp.Api;
using UnifiedStockExchange.Sdk.CSharp.Model;

namespace UnifiedStockExchange.CSharp.Tests
{
    [TestClass]
    public class PriceWriterTests
    {
        [TestMethod]
        public async Task ConnectSendPriceUpdateAndReadHistory()
        {
            //WARNING: This test may fail if run twice withing the same minute.

            ///// Arrange /////

            AppSettings appSettings = TestUtility.GetAppSettings();
            PriceWriter priceWriter = new PriceWriter(
                appSettings.UnifiedStockExchangeUrl, 
                appSettings.ExchangeName, 
                appSettings.Quote.ToTradingPair()
            );
            PriceHistoryApi historyApi = new PriceHistoryApi(appSettings.UnifiedStockExchangeUrl);
            DateTime dateTimeNow = DateTime.UtcNow;
            decimal price = 15000;
            decimal amount = 50;

            ///// Act /////
            await priceWriter.ConnectAsync();
            await priceWriter.SendPriceUpdate(dateTimeNow, price, amount);
            await priceWriter.SendPriceUpdate(dateTimeNow.AddMinutes(1), price + 100, amount * 1.1M);

            List<PriceCandle> priceHistory = historyApi.PriceHistoryGetHistoryDataFromPost(
                appSettings.ExchangeName,
                appSettings.Quote,
                dateTimeNow, 
                1, 
                SampleInterval.OneMinute
            );

            ///// Assert /////

            Assert.AreEqual(dateTimeNow.TruncateByInterval(SampleInterval.OneMinute), priceHistory[0].Date);
            Assert.AreEqual(price, (decimal)priceHistory[0].Open);
            Assert.AreEqual(amount, (decimal)priceHistory[0].Volume);
        }

        [TestMethod]
        public async Task ConnectSendPriceWaitCacheAndReadHistory()
        {
            //WARNING: This test waits for the server to flush price cache and takes a lot of time to complete.

            ///// Arrange /////

            AppSettings appSettings = TestUtility.GetAppSettings();
            PriceWriter priceWriter = new PriceWriter(
                appSettings.UnifiedStockExchangeUrl,
                appSettings.ExchangeName,
                appSettings.Quote.ToTradingPair()
            );
            PriceHistoryApi historyApi = new PriceHistoryApi(appSettings.UnifiedStockExchangeUrl);
            DateTime dateTimeNow = DateTime.UtcNow;
            decimal price = 15000;
            decimal amount = 50;

            ///// Act /////
            await priceWriter.ConnectAsync();
            await priceWriter.SendPriceUpdate(dateTimeNow, price, amount);
            await priceWriter.SendPriceUpdate(dateTimeNow.AddMinutes(1), price + 100, amount * 1.1M);

            await Task.Delay(TimeSpan.FromSeconds(10 * 60 + 1));
            List<PriceCandle> priceHistory = historyApi.PriceHistoryGetHistoryDataFromPost(
                appSettings.ExchangeName,
                appSettings.Quote,
                dateTimeNow,
                1,
                SampleInterval.OneMinute
            );

            ///// Assert /////

            Assert.AreEqual(dateTimeNow.TruncateByInterval(SampleInterval.OneMinute), priceHistory[0].Date);
            Assert.AreEqual(price, (decimal)priceHistory[0].Open);
            Assert.AreEqual(amount, (decimal)priceHistory[0].Volume);
        }
    }
}