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

public class CoinMarketCapForwarder : IDisposable
{
    private ClientWebSocket _webSocket;
    private PriceWriter _priceWriter;
    private readonly string _exchangeName;
    private readonly IList<int> _currencyIds;
    private readonly Uri _coinMarketCapWs;
    private readonly Uri _unifiedExchangeWs;

    public CoinMarketCapForwarder(string exchangeName, IList<int> currencyIds, Uri coinMarketCapWs, Uri unifiedExchangeWs)
    {
        _exchangeName = exchangeName;
        _currencyIds = currencyIds;
        _coinMarketCapWs = coinMarketCapWs;
        _unifiedExchangeWs = unifiedExchangeWs;
    }

    public async Task ConnectAndProcessDataAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CoinMarketCapForwarder));

        _priceWriter = new PriceWriter(_unifiedExchangeWs.AbsoluteUri, _exchangeName);
        try
        {
            await _priceWriter.ConnectAsync();
        }
        catch(WebSocketException e)
        {
            throw new ApplicationException("Could not connect to UnifiedStockExchange WebSocket: " + e.Message);
        }

        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/113.0");
        try
        {
            await _webSocket.ConnectAsync(_coinMarketCapWs, CancellationToken.None);
        }
        catch(WebSocketException e)
        {
            throw new ApplicationException("Could not connect to CoinMarketCap WebSocket: " + e.Message);
        }

        try
        {
            await ProcessWebSocketDataAsync();
        }
        finally
        {
            _webSocket.Dispose();
            _priceWriter.Dispose();
        }
    }

    private async Task ProcessWebSocketDataAsync()
    {
        // Construct the subscription message
        var subscriptionMessage = new
        {
            method = "RSUBSCRIPTION",
            @params = new[] { "main-site@crypto_price_5s@{}@normal", String.Join(",", _currencyIds) }
        };

        // Send the subscription message
        await SendMessageAsync(JsonConvert.SerializeObject(subscriptionMessage));

        // Discard the first receive message
        await ReceiveMessageAsync();

        // Process subsequent receive messages
        while (_webSocket.State == WebSocketState.Open)
        {
            await ReceiveAndForwardPriceAsync();
        }
    }

    private async Task ReceiveAndForwardPriceAsync()
    {
        var message = await ReceiveMessageAsync();

        var dataObject = JsonConvert.DeserializeObject<JObject>(message);

        var currencyId = dataObject["d"]["id"].Value<int>();
        var price = dataObject["d"]["p"].Value<double>();
        var totalVolume = dataObject["d"]["mc"].Value<double>();
        var unixTimeMillis = long.Parse(dataObject["t"].Value<string>());

        await SendPriceUpdateAsync(currencyId, price, totalVolume, unixTimeMillis);
    }

    private async Task SendMessageAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task<string> ReceiveMessageAsync()
    {
        byte[] buffer = new byte[1024];
        MemoryStream memory = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            memory.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        memory.Seek(0, SeekOrigin.Begin);
        return Encoding.UTF8.GetString(new ArraySegment<byte>(memory.GetBuffer(), 0, (int)memory.Length));
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
                _totalVolume[currencyId] = currentVolume;
            }
        }

        double amount = currentVolume - lastVolume;
        amount = amount < 0 ? 0 : amount;

        ValueTuple<string, string> tradingPair = ("USD", Program.CryptoCurrencies[currencyId].Symbol);
        DateTime time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixTimeMillis);
        await _priceWriter.SendPriceUpdateAsync(tradingPair, time, (decimal)price, (decimal)amount);
    }

    bool _isDisposed;
    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _webSocket?.Dispose();
        _priceWriter?.Dispose();
    }
}
