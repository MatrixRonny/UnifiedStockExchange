namespace UnifiedStockExchange.Contracts
{
    public delegate Task PriceUpdateHandler(string tradingPair, DateTime time, decimal price, decimal amount);
}
