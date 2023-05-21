using ServiceStack.OrmLite;
using System.Data;
using System.Data.SQLite;
using System.Reflection;
using UnifiedStockExchange.Exceptions;

namespace UnifiedStockExchange.DataAccess
{
    public class TableDataAccess<T> : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly string _tableName;
        IOrmLiteDialectProvider _dialectProvider;

        public TableDataAccess(OrmLiteConnectionFactory connectionFactory, string tableName)
        {
            _connection = connectionFactory.CreateDbConnection();
            _connection.TableAlias(tableName);
            _tableName = tableName;
            _dialectProvider = connectionFactory.DialectProvider;
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

            _connection.CreateTable<T>(_tableName, overwrite);
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

            _connection.DropTable<T>(_tableName);
        }

        public bool TableExists()
        {
            lock (_connection)
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    _connection.Open();
                }
            }

            return _connection.TableExists(_tableName);
        }

        public SelectFilter<T> CreateSelectFilter()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            if (!TableExists())
                throw new DataAccessException("Could not find table with name " + _tableName);

            return new SelectFilter<T>(_connection, _tableName);
        }

        public DeleteFilter<T> CreateDeleteFilter()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            if (!TableExists())
                throw new DataAccessException("Could not find table with name " + _tableName);

            return new DeleteFilter<T>(_connection, _tableName);
        }

        public UpdateFilter<T> CreateUpdateFilter()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            if (!TableExists())
                throw new DataAccessException("Could not find table with name " + _tableName);

            return new UpdateFilter<T>(_connection, _tableName);
        }

        public void Insert(T entity)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TableDataAccess<T>));

            if (!TableExists())
                throw new DataAccessException("Could not find table with name " + _tableName);

            IDbCommand sqlCmd = _connection.CreateCommand();
            _dialectProvider.PrepareParameterizedInsertStatement<T>(sqlCmd, tableName: _tableName);
            _dialectProvider.SetParameterValues<T>(sqlCmd, entity);

            try
            {
                sqlCmd.ExecuteNonQuery();
            }
            catch(SQLiteException e)
            {
                if(e.ErrorCode == 19)
                {
                    throw new ConstraintViolationException(e.Message, e);
                }
            }
        }

        bool _isDisposed;
        public void Dispose()
        {
            _isDisposed = true;
            _connection.Close();
        }
    }
}
