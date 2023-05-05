using ServiceStack.OrmLite;
using System.Data;
using System.Linq.Expressions;

namespace UnifiedStockExchange.DataAccess
{
    public class UpdateFilter<T>
    {
        readonly SqlExpression<T> _sqlExpression;
        private IDbConnection _connection;

        public UpdateFilter(IDbConnection connection, string tableName)
        {
            _connection = connection;
            _sqlExpression = _connection.From<T>();
            _sqlExpression.ModelDef.Alias = tableName;
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
            sqlCmd.ExecNonQuery();
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
            sqlCmd.ExecNonQuery();
        }
    }
}
