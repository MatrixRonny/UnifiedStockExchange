using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        ILogger logger = CreateLogger();

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
                logger.LogError(e, "Main loop exception.");
            }

            await Task.Delay(5000);
        }
    }

    private static ILogger CreateLogger()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole();

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            builder.AddFile(config.GetSection("Logging"), options =>
            {
                options.FormatLogEntry = msg =>
                {
                    string logLevel = GetShortLogLevel(msg.LogLevel);
                    if (msg.Exception == null)
                        return $"{DateTime.Now.ToString("O")}\t{logLevel}\t[{msg.LogName}]\t[{msg.EventId.Name ?? "0"}]\t{msg.Message}";
                    else
                        return $"{DateTime.Now.ToString("O")}\t{logLevel}\t[{msg.LogName}]\t[{msg.EventId.Name ?? "0"}]\t{msg.Message} => {msg.Exception.GetType().FullName}: {msg.Exception.Message}\r\n{msg.Exception.StackTrace}";
                };
            });
        });

        return services.BuildServiceProvider().GetService<ILogger<Program>>();
    }

    static string GetShortLogLevel(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                return "TRCE";
            case LogLevel.Debug:
                return "DBUG";
            case LogLevel.Information:
                return "INFO";
            case LogLevel.Warning:
                return "WARN";
            case LogLevel.Error:
                return "FAIL";
            case LogLevel.Critical:
                return "CRIT";
            default:
                return logLevel.ToString().ToUpper();
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