using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System.Data;
using UnifiedStockExchange.Domain.Entities;

namespace UnifiedStockExchange.Controllers
{
    public class TestController : ApiControllerBase
    {
        private readonly IDbConnection _dbConnection;

        public TestController(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpGet]
        public string Test()
        {
            //// SELECT with LINQ
            var sqlExpr = _dbConnection.From<PriceCandle>().From("Digifinex_BTC-USDT").Where(it => it.Open > 5000);
            string sql = sqlExpr.ToSelectStatement();

            //// DELETE with WHERE
            //var result = _dbConnection.From<PriceCandle>().Where(it => it.Open > 5000);
            //result.ModelDef.Name = "Digifinex_BTC-USDT";
            //var sqlCmd = _dbConnection.CreateCommand();
            //sqlCmd.CommandText = result.ToDeleteRowStatement();
            //foreach (IDbDataParameter param in result.Params)
            //{
            //    sqlCmd.Parameters.Add(param);
            //}

            //_dbConnection.Open();
            //sqlCmd.ExecNonQuery();
            //_dbConnection.Close();

            // INSERT to specific table
            IDbCommand sqlCmd2 = _dbConnection.CreateCommand();
            SqliteOrmLiteDialectProvider.Instance.PrepareParameterizedInsertStatement<PriceCandle>(sqlCmd2);
            ((IDbDataParameter)sqlCmd2.Parameters["@" + nameof(PriceCandle.Open)]).Value = 1234;
            string sql2 = sqlCmd2.CommandText.ReplaceFirst(nameof(PriceCandle), "Digifinex_BTC-USDT");

            return "Yaay!";
        }
    }
}
