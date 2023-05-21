namespace UnifiedStockExchange.Exceptions
{
    public class ConstraintViolationException : DataAccessException
    {
        public ConstraintViolationException(string? message) : base(message)
        {
        }

        public ConstraintViolationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
