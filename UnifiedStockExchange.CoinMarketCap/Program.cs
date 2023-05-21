using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using UnifiedStockExchange.CoinMarketCap;

internal class Program
{
    public const string ExchangeName = "CoinMarketCap";
    public const string WebSocketUrl = "wss://push.coinmarketcap.com/ws?device=web&client_source=home_page";

    public static IReadOnlyDictionary<int, CryptoCurrency> CryptoCurrencies { get; private set; } = null!;

    private static async Task Main(string[] args)
    {
        if(args.Length < 1)
        {
            Console.WriteLine("Usage: CoinMarketCap <UnifiedStockExchangeUrl>");
            return;
        }

        string priceInfoJson = File.ReadAllText("CoinMarketCap.json");
        List<CryptoCurrency> currencies = GetCryptoCurrencies(priceInfoJson);
        CryptoCurrencies = currencies.ToDictionary(it => it.Id, it => it).ToImmutableDictionary();

        while(true)
        {
            var priceForwarder = new CoinMarketCapForwarder(ExchangeName, new int[] { 1 }, new Uri(WebSocketUrl), new Uri(args[0]));
            try
            {
                await priceForwarder.ConnectAndProcessDataAsync();
            }
            catch(Exception e)
            {
                Console.WriteLine($"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    private static List<CryptoCurrency> GetCryptoCurrencies(string priceInfo)
    {
        List<CryptoCurrency> cryptoCurrencies = new List<CryptoCurrency>();

        JObject json = JObject.Parse(priceInfo);
        JArray data = (JArray)json["cryptocurrency"]["listingLatest"]["data"];

        for (int i = 1; i < data.Count; i++)
        {
            JArray item = (JArray)data[i];

            if (item[0].Type == JTokenType.Float && item[1].Type == JTokenType.Float && item[36].Type == JTokenType.String)
            {
                CryptoCurrency cryptoCurrency = new CryptoCurrency
                {
                    Id = (int)item[6],
                    Name = (string)item[13],
                    Symbol = (string)item[36]
                };

                cryptoCurrencies.Add(cryptoCurrency);
            }
        }

        return cryptoCurrencies;
    }
}