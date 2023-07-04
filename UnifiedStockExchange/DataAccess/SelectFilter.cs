using ServiceStack.OrmLite;
using System.Data;
using System.Linq.Expressions;

namespace UnifiedStockExchange.DataAccess
{
    public class SelectFilter<T>
    {
        readonly SqlExpression<T> _sqlExpression;
        readonly IDbConnection _connection;

        public SelectFilter(IDbConnection connection, string tableName)
        {
            _connection = connection;
            _sqlExpression = _connection.From<T>(tableName);
        }

        private SelectFilter(SqlExpression<T> sqlExpression, IDbConnection connection)
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

            return sqlCmd.ExecuteReader().ConvertToList<T>(_sqlExpression.DialectProvider);
        }
    }
}
