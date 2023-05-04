using Microsoft.AspNetCore.Mvc;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Domain.Enums;
using static ServiceStack.OrmLite.Dapper.SqlMapper;
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
        public IList<PriceCandle> Test()
        {
            TableDataAccess<PriceCandle> btcUsdtPrice = new TableDataAccess<PriceCandle>(_connectionFactory, "CoinMarketCap_BTC-USDT");

            //FAIL
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

    public class TableDataAccess<T> : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly string _tableName;

        public TableDataAccess(OrmLiteConnectionFactory connectionFactory, string tableName)
        {
            _connection = connectionFactory.CreateDbConnection();
            _tableName = tableName;
        }

        public void CreateTable(bool overwrite = false)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            _connection.Exec(dbCmd => OrmLiteWriteCommandExtensions.CreateTable<T>(dbCmd, _tableName, overwrite));
        }

        public void DropTable()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            _connection.Exec(dbCmd => OrmLiteWriteCommandExtensions.DropTable<T>(dbCmd, _tableName));
        }

        public SelectFilter<T> CreateSelectFilter()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            return new SelectFilter<T>(_connection, _tableName);
        }

        public DeleteFilter<T> CreateDeleteFilter()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            return new DeleteFilter<T>(_connection, _tableName);
        }

        public UpdateFilter<T> CreateUpdateFilter()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            return new UpdateFilter<T>(_connection, _tableName);
        }

        public void Insert(T entity)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            IDbCommand sqlCmd = _connection.CreateCommand();
            SqliteOrmLiteDialectProvider.Instance.PrepareParameterizedInsertStatement<T>(sqlCmd);
            //sqlCmd.CommandText = sqlCmd.CommandText.Replace($"UPDATE ${nameof(T)}", "UPDATE ")

            PropertyInfo[] allProps = typeof(T).GetProperties();
            IEnumerable<PropertyInfo> publicPropsWithGetter = allProps
                .Where(it => it.CanRead && it.GetGetMethod(false) != null);
            foreach (PropertyInfo prop in publicPropsWithGetter)
            {
                ((IDbDataParameter)sqlCmd.Parameters["@" + prop.Name]).Value = prop.GetValue(entity);
            }

            //string insertStatement = sqlCmd.CommandText.ReplaceFirst(nameof(T), _tableName);
            sqlCmd.ExecuteNonQuery();
        }

        bool _isDisposed;
        public void Dispose()
        {
            _isDisposed = true;
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
            foreach (var dbParam in _sqlExpression.Params)
            {
                sqlCmd.Parameters.Add(dbParam);
            }

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

        public UpdateFilter(SqlExpression<T> sqlExpression, IDbConnection connection)
        {
            _sqlExpression = sqlExpression;
            _connection = connection;
        }

        public UpdateFilter<T> Where(Expression<Func<T, bool>> predicate)
        {
            return new UpdateFilter<T>(_sqlExpression.Where(predicate), _connection);
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

        public void ExecuteUpdate(Dictionary<string, object> updateFields)
        {
            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            IDbCommand sqlCmd = _connection.CreateCommand();
            _sqlExpression.PrepareUpdateStatement(sqlCmd, updateFields);
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
            foreach (var dbParam in _sqlExpression.Params)
            {
                sqlCmd.Parameters.Add(dbParam);
            }

            OrmLiteResultsFilterExtensions.ExecNonQuery(sqlCmd);
        }
    }
}
