using ServiceStack.OrmLite;
using System.Data;
using System.Reflection;

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
            _dialectProvider.PrepareParameterizedInsertStatement<T>(sqlCmd);

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
}
