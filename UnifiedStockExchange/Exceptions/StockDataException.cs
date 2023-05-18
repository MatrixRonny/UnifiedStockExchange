namespace UnifiedStockExchange.Exceptions
{
    public class StockDataException : ApplicationException
    {
        public StockDataException(string? message) : base(message)
        {
        }

        public StockDataException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
