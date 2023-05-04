using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using UnifiedStockExchange.Domain.Entities;
using OrmLiteResultsFilterExtensions = UnifiedStockExchange.OrmLiteInternals.OrmLiteResultsFilterExtensions;
using OrmLiteUtils = UnifiedStockExchange.OrmLiteInternals.OrmLiteUtils;
using OrmLiteWriteCommandExtensions = UnifiedStockExchange.OrmLiteInternals.OrmLiteWriteCommandExtensions;

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

    public class TableDataAccess<T> : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly string _tableName;

        public TableDataAccess(OrmLiteConnectionFactory connectionFactory, string tableName)
        {
            _connection = connectionFactory.CreateDbConnection();
            _tableName = tableName;
        }

        public void CreateTable(string tableName, bool overwrite = false)
        {
            _connection.Exec(dbCmd => OrmLiteWriteCommandExtensions.CreateTable<T>(dbCmd, tableName));
        }

        public SelectFilter<T> CreateSelectFilter()
        {
            return new SelectFilter<T>(_connection, _tableName);
        }

        public DeleteFilter<T> CreateDeleteFilter()
        {
            return new DeleteFilter<T>(_connection, _tableName);
        }

        public UpdateFilter<T> CreateUpdateFilter()
        {
            return new UpdateFilter<T>(_connection, _tableName);
        }

        public void Insert(T entity)
        {
            IDbCommand sqlCmd = _connection.CreateCommand();

            SqliteOrmLiteDialectProvider.Instance.PrepareParameterizedInsertStatement<T>(sqlCmd);
            foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.GetProperty))
            {
                ((IDbDataParameter)sqlCmd.Parameters["@" + prop.Name]).Value = prop.GetValue(entity);
            }

            //string insertStatement = sqlCmd.CommandText.ReplaceFirst(nameof(T), _tableName);
            sqlCmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            _connection.Close();
        }
    }

    public class SelectFilter<T>
    {
        readonly SqlExpression<T> _sqlExpression;
        readonly IDbConnection _connection;

        public SelectFilter(IDbConnection connection, string tableName)
        {
            _connection = connection;
            _sqlExpression = _connection.From<T>(tableName);
        }

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
            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            string selectStatement = _sqlExpression.ToSelectStatement();

            IDbCommand sqlCmd = _connection.CreateCommand();
            sqlCmd.CommandText = selectStatement;
            return OrmLiteUtils.ConvertToList<T>(sqlCmd.ExecuteReader(), _sqlExpression.DialectProvider);
        }
    }

    public class UpdateFilter<T>
    {
        readonly SqlExpression<T> _sqlExpression;
        private IDbConnection _connection;

        public UpdateFilter(IDbConnection connection, string tableName)
        {
            _connection = connection;
            _sqlExpression = _connection.From<T>();
            _sqlExpression.ModelDef.Name = tableName;
        }

        public void ExecuteUpdate(T entity)
        {
            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            IDbCommand sqlCmd = _connection.CreateCommand();
            _sqlExpression.PrepareUpdateStatement(sqlCmd, entity);
            OrmLiteResultsFilterExtensions.ExecNonQuery(sqlCmd);
        }
    }

    public class DeleteFilter<T>
    {
        readonly SqlExpression<T> _sqlExpression;
        readonly IDbConnection _connection;

        public DeleteFilter(IDbConnection connection, string tableName)
        {
            _connection = connection;
            _sqlExpression = _connection.From<T>();
            _sqlExpression.ModelDef.Name = tableName;
        }

        public DeleteFilter(SqlExpression<T> sqlExpression, IDbConnection connection)
        {
            _sqlExpression = sqlExpression;
            _connection = connection;
        }

        public DeleteFilter<T> Where(Expression<Func<T, bool>> predicate)
        {
            return new DeleteFilter<T>(_sqlExpression.Where(predicate), _connection);
        }

        public void ExecuteDelete()
        {
            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            string deleteStatement = _sqlExpression.ToDeleteRowStatement();

            IDbCommand sqlCmd = _connection.CreateCommand();
            sqlCmd.CommandText = deleteStatement;
            OrmLiteResultsFilterExtensions.ExecNonQuery(sqlCmd);
        }
    }
}
