namespace UnifiedStockExchange.Utility
{
    public static class ExtensionMethods
    {
        public static string ToPairString(this ValueTuple<string, string> tradingPair)
        {
            return $"{tradingPair.Item2}-{tradingPair.Item1}";
        }

        public static string ToExchangeQuote(this ValueTuple<string, string> tradingPair, string exchangeName)
        {
            return $"{exchangeName}|{tradingPair.Item2}-{tradingPair.Item1}";
        }

        public static ValueTuple<string, string> ToTradingPair(this string quote)
        {
            string[] split = quote.ToUpper().Split('-');
            return (split[1], split[0]);
        }

        public static ValueTuple<string, ValueTuple<string, string>> ToExchangeAndTradingPair(this string exchangeQuote)
        {
            string[] split = exchangeQuote.Split('|');
            var tradingPair = split[1].ToTradingPair();
            return (split[0], tradingPair);
        }
    }
}
