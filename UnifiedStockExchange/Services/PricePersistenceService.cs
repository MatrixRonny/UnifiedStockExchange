using ServiceStack.OrmLite;
using System.Data;

namespace UnifiedStockExchange.Services
{
    public class PricePersistenceService
    {
        private readonly IDbConnection _dbConnection;

        Dictionary<string, DateTime> _lastWrite = new();
        Dictionary<string, object> _tableAccess = new();
        public PricePersistenceService(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        DateTime _lastCleanup = DateTime.UtcNow;
        public void RecordPrice(string exchangeName, ValueTuple<string,string> tradingPair, DateTime time, decimal price, decimal amount)
        {
            string exchangeQuote = $"{exchangeName}|{tradingPair.Item1}-{tradingPair.Item2}";
            if (!_lastWrite.ContainsKey(exchangeQuote))
            {
                _lastWrite[exchangeQuote] = DateTime.UtcNow;
                //Create data access instance to access table exchangeQuote of type CandleData. 
            }

            //TODO: Write data to table identified by exchangeQuote.

            if((DateTime.UtcNow - _lastCleanup).TotalMinutes > 10)
            {
                _lastWrite.Remove(exchangeQuote);
                _tableAccess.Remove(exchangeQuote);
            }
        }
    }
}
