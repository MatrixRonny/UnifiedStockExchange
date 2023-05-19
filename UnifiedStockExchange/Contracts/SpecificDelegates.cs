namespace UnifiedStockExchange.Contracts
{
    public delegate void PriceUpdateHandler(DateTime time, decimal price, decimal amount);
}
