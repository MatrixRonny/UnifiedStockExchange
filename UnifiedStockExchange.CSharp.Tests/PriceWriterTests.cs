using System.Text.Json.Nodes;
using UnifiedStockExchangeSdk.CSharp.Api;
using UnifiedStockExchangeSdk.CSharp.Model;

namespace UnifiedStockExchange.CSharp.Tests
{
    [TestClass]
    public class PriceWriterTests
    {
        [TestMethod]
        public async Task ConnectAndSendSinglePriceUpdate()
        {
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

            List<PriceCandle> priceHistory = historyApi.PriceHistoryGetHistoryDataFromPost(
                appSettings.ExchangeName, 
                dateTimeNow, 
                1, 
                SampleInterval.OneMinute
            );

            ///// Assert /////

            Assert.AreEqual(dateTimeNow, priceHistory[0].Date);
            Assert.AreEqual(price, priceHistory[0].Open);
            Assert.AreEqual(amount, priceHistory[0].Volume);
        }
    }
}