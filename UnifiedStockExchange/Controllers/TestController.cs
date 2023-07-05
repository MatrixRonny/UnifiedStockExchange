using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.DataAccess;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Domain.Enums;

namespace UnifiedStockExchange.Controllers
{
    public class TestController : ApiControllerBase
    {
        private readonly OrmLiteConnectionFactory _connectionFactory;

        public TestController(OrmLiteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [HttpGet]
        public IList<PriceCandle> Test()
        {
            //_connectionFactory.OpenDbConnection()
            TableDataAccess<PriceCandle> btcUsdtPrice = new TableDataAccess<PriceCandle>(_connectionFactory, "CoinMarketCap_BTC-USDT");
            TableDataAccess<PriceCandle> ethUsdtPrice = new TableDataAccess<PriceCandle>(_connectionFactory, "CoinMarketCap_ETH-USDT");

            //OK
            btcUsdtPrice.CreateTable(true);
            ethUsdtPrice.CreateTable(true);

            //OK
            btcUsdtPrice.Insert(new PriceCandle
            {
                Open = 1,
                High = 2000,
                Low = 1,
                Close = 1000,
                Interval = SampleInterval.OneMinute,
                Date = DateTime.UtcNow
            });
            ethUsdtPrice.Insert(new PriceCandle
            {
                Open = 1,
                High = 200,
                Low = 1,
                Close = 100,
                Interval = SampleInterval.OneMinute,
                Date = DateTime.UtcNow
            });
            btcUsdtPrice.Insert(new PriceCandle
            {
                Open = 1000,
                High = 4000,
                Low = 500,
                Close = 500,
                Interval = SampleInterval.OneMinute,
                Date = DateTime.UtcNow
            });
            ethUsdtPrice.Insert(new PriceCandle
            {
                Open = 100,
                High = 400,
                Low = 50,
                Close = 50,
                Interval = SampleInterval.OneMinute,
                Date = DateTime.UtcNow
            });

            //OK
            btcUsdtPrice.CreateUpdateFilter().Where(it => it.Close == 500)
                .ExecuteUpdate(new PriceCandle
                {
                    Open = 1000,
                    High = 4000,
                    Low = 500,
                    Close = 700,
                    Interval = SampleInterval.OneMinute,
                    Date = DateTime.UtcNow
                });
            ethUsdtPrice.CreateUpdateFilter().Where(it => it.Close == 500)
                .ExecuteUpdate(new PriceCandle
                {
                    Open = 100,
                    High = 400,
                    Low = 50,
                    Close = 70,
                    Interval = SampleInterval.OneMinute,
                    Date = DateTime.UtcNow
                });

            //OK
            btcUsdtPrice.CreateDeleteFilter().Where(it => it.Open == 1).ExecuteDelete();
            ethUsdtPrice.CreateDeleteFilter().Where(it => it.Open == 1).ExecuteDelete();

            //OK
            var result1 = btcUsdtPrice.CreateSelectFilter().Where(it => it.Open > 500).ExecuteSelect();
            var result2 = ethUsdtPrice.CreateSelectFilter().Where(it => it.Open > 50).ExecuteSelect();

            //OK
            btcUsdtPrice.DropTable();
            ethUsdtPrice.DropTable();

            return result1.Union(result2).ToArray();
        }
    }
}
