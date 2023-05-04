using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using UnifiedStockExchange.Domain.Entities;

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
        public string Test()
        {
            IDbConnection dbConnection = _connectionFactory.CreateDbConnection();

            //// SELECT with LINQ
            var sqlExpr = dbConnection.From<PriceCandle>().From("Digifinex_BTC-USDT").Where(it => it.Open > 5000);
            string sql = sqlExpr.ToSelectStatement();

            //// DELETE with WHERE
            var result = dbConnection.From<PriceCandle>().Where(it => it.Open > 5000);
            result.ModelDef.Name = "Digifinex_BTC-USDT";
            var sqlCmd = dbConnection.CreateCommand();
            sqlCmd.CommandText = result.ToDeleteRowStatement();

            //_dbConnection.Open();
            //sqlCmd.ExecNonQuery();
            //_dbConnection.Close();

            // INSERT to specific table
            IDbCommand sqlCmd2 = dbConnection.CreateCommand();
            SqliteOrmLiteDialectProvider.Instance.PrepareParameterizedInsertStatement<PriceCandle>(sqlCmd2);
            ((IDbDataParameter)sqlCmd2.Parameters["@" + nameof(PriceCandle.Open)]).Value = 1234;
            string sql2 = sqlCmd2.CommandText.ReplaceFirst(nameof(PriceCandle), "Digifinex_BTC-USDT");

            return "Yaay!";
        }
    }

    public class TableDataAccess<T>
    {
        private readonly OrmLiteConnectionFactory _connectionFactory;
        private readonly string _tableName;

        public TableDataAccess(OrmLiteConnectionFactory connectionFactory, string tableName)
        {
            _connectionFactory = connectionFactory;
            _tableName = tableName;
        }

        public SelectFilter<T> CreateFilter()
        {
            IDbConnection dbConnection = _connectionFactory.CreateDbConnection();
            return new SelectFilter<T>(dbConnection.From<T>().From(_tableName), dbConnection);
        }

        public void Insert(T entity)
        {
            IDbConnection dbConnection = _connectionFactory.CreateDbConnection();
            IDbCommand sqlCmd = dbConnection.CreateCommand();
            SqliteOrmLiteDialectProvider.Instance.PrepareParameterizedInsertStatement<T>(sqlCmd);

            foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.GetProperty))
            {
                ((IDbDataParameter)sqlCmd.Parameters["@" + prop.Name]).Value = prop.GetValue(entity);
            }

            //string insertStatement = sqlCmd.CommandText.ReplaceFirst(nameof(T), _tableName);
            sqlCmd.ExecuteNonQuery();
        }
    }

    public class SelectFilter<T>
    {
        private readonly SqlExpression<T> _sqlExpression;
        private readonly IDbConnection _connection;

        public SelectFilter(SqlExpression<T> sqlExpression, IDbConnection connection)
        {
            _sqlExpression = sqlExpression;
            _connection = connection;
        }

        public SelectFilter<T> Where(Expression<Func<T, bool>> predicate)
        {
            return new SelectFilter<T>(_sqlExpression.Where(predicate), _connection);
        }

        public SelectFilter<T> OrderBy(Expression<Func<T, object>> selector)
        {
            return new SelectFilter<T>(_sqlExpression.OrderBy(selector), _connection);
        }

        public SelectFilter<T> ThenOrderBy(Expression<Func<T, object>> selector)
        {
            return new SelectFilter<T>(_sqlExpression.ThenBy(selector), _connection);
        }

        public SelectFilter<T> Distinct(Expression<Func<T, object>> selector)
        {
            return new SelectFilter<T>(_sqlExpression.SelectDistinct(selector), _connection);
        }

        public SelectFilter<T> Skip(int count)
        {
            return new SelectFilter<T>(_sqlExpression.Skip(count), _connection);
        }

        public SelectFilter<T> Take(int count)
        {
            return new SelectFilter<T>(_sqlExpression.Take(count), _connection);
        }

        public IList<T> ExecuteSelect()
        {
            using (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }

                string selectStatement = _connection.CreateCommand().CommandText = _sqlExpression.ToSelectStatement();

                IDbCommand sqlCmd = _connection.CreateCommand();
                sqlCmd.Connection = _connection;
                sqlCmd.CommandText = selectStatement;
                return sqlCmd.ExecuteReader().ConvertToList<T>(_sqlExpression.DialectProvider);
            }
        }
    }

    public class DeleteFilter<T>
    {
        private readonly SqlExpression<T> _sqlExpression;

        public DeleteFilter(SqlExpression<T> sqlExpression)
        {
            _sqlExpression = sqlExpression;
        }

        public DeleteFilter<T> Where(Expression<Func<T, bool>> predicate)
        {
            return new DeleteFilter<T>(_sqlExpression.Where(predicate));
        }

        public void ExecuteDelete()
        {
            throw new NotImplementedException();
        }
    }
}
