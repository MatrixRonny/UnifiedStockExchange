using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnifiedStockExchange.CSharp;

public class CoinMarketCapForwarder
{
    private readonly ClientWebSocket _webSocket;
    private readonly Dictionary<int, PriceWriter> _priceWriters;
    private readonly IList<int> _currencyIds;
    private readonly Uri _coinMarketCapWs;

    public CoinMarketCapForwarder(string exchangeName, IList<int> currencyIds, Uri coinMarketCapWs, Uri unifiedExchangeWs)
    {
        _currencyIds = currencyIds;
        _coinMarketCapWs = coinMarketCapWs;

        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/113.0");

        _priceWriters = _currencyIds.Select(it => Program.CryptoCurrencies[it])
            .ToDictionary(
                it => it.Id,
                it => new PriceWriter(unifiedExchangeWs.AbsoluteUri, exchangeName, ("USD", it.Symbol))
            );
    }

    public async Task ConnectAndProcessData()
    {
        await _webSocket.ConnectAsync(_coinMarketCapWs, CancellationToken.None);
        foreach(PriceWriter priceWriter in _priceWriters.Values)
        {
            await priceWriter.ConnectAsync();
        }

        // Construct the subscription message
        var subscriptionMessage = new
        {
            method = "RSUBSCRIPTION",
            @params = new[] { "main-site@crypto_price_5s@{}@normal", GetCurrencyIdsString() }
        };

        // Send the subscription message
        await SendMessageAsync(JsonConvert.SerializeObject(subscriptionMessage));

        // Discard the first receive message
        await ReceiveMessageAsync();

        // Process subsequent receive messages
        while (_webSocket.State == WebSocketState.Open)
        {
            var message = await ReceiveMessageAsync();
            await ProcessDataAsync(message);
        }

        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
    }

    private string GetCurrencyIdsString()
    {
        return string.Join(",", _currencyIds);
    }

    private async Task SendMessageAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task<string> ReceiveMessageAsync()
    {
        var buffer = new byte[1024];
        var message = new StringBuilder();

        WebSocketReceiveResult result;
        do
        {
            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var data = Encoding.UTF8.GetString(buffer, 0, result.Count);
            message.Append(data);
        } while (!result.EndOfMessage);

        return message.ToString();
    }

    private async Task ProcessDataAsync(string message)
    {
        var dataObject = JsonConvert.DeserializeObject<JObject>(message);

        var currencyId = dataObject["d"]["id"].Value<int>();
        var price = dataObject["d"]["p"].Value<double>();
        var totalVolume = dataObject["d"]["mc"].Value<double>();
        var unixTimeMillis = long.Parse(dataObject["t"].Value<string>());

        await SendPriceUpdateAsync(currencyId, price, totalVolume, unixTimeMillis);
    }

    Dictionary<int, double> _totalVolume = new Dictionary<int, double>();
    private async Task SendPriceUpdateAsync(int currencyId, double price, double currentVolume, long unixTimeMillis)
    {
        double lastVolume;
        lock(_totalVolume)
        {
            if(!_totalVolume.ContainsKey(currencyId))
            {
                lastVolume = _totalVolume[currencyId] = currentVolume;
            }
            else
            {
                lastVolume = _totalVolume[currencyId];
            }
        }

        double amount = currentVolume - lastVolume;
        amount = amount < 0 ? 0 : amount;

        await _priceWriters[currencyId].SendPriceUpdate(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixTimeMillis), (decimal)price, (decimal)amount);
    }
}
