namespace UnifiedStockExchange.Contracts
{
    public delegate void PriceUpdateHandler(string tradingPair, DateTime time, decimal price, decimal amount);
}
