namespace UnifiedStockExchange.Utility
{
    public static class ExtensionMethods
    {
        public static string ToQuote(this ValueTuple<string, string> tradingPair)
        {
            return $"{tradingPair.Item2}-{tradingPair.Item1}";
        }

        public static string ToExchangeQuote(this ValueTuple<string, string> tradingPair, string exchangeName)
        {
            return $"{exchangeName}|{tradingPair.Item2}-{tradingPair.Item1}";
        }

        public static ValueTuple<string, string> ToTradingPair(this string quote)
        {
            string[] split = quote.Split('-');
            return (split[1], split[0]);
        }
    }
}
