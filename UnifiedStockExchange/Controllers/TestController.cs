using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.DataAccess;
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

            //OK
            btcUsdtPrice.CreateTable(true);

            //FAIL
            btcUsdtPrice.Insert(new PriceCandle
            {
                Open = 1,
                High = 2000,
                Low = 1,
                Close = 1000,
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

            //OK
            btcUsdtPrice.CreateDeleteFilter().Where(it => it.Open == 10).ExecuteDelete();

            //OK
            var result = btcUsdtPrice.CreateSelectFilter().Where(it => it.Open > 500).ExecuteSelect();

            //FAIL
            btcUsdtPrice.DropTable();

            return result;
        }
    }
}
