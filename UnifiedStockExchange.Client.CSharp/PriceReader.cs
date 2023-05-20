using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnifiedStockExchange.CSharp;
using System.IO;

namespace UnifiedStockExchange.Client.CSharp
{
    public class PriceReader : IDisposable
    {
        private readonly string _unifiedStockExchangeUrl;
        private readonly string _exchangeName;
        private readonly (string, string) _tradingPair;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _tokenSource;

        public event Action<PriceUpdateWithDate> PriceUpdateReceived;

        public PriceReader(string unifiedStockExchangeUrl, string exchangeName, ValueTuple<string, string> tradingPair)
        {
            _unifiedStockExchangeUrl = unifiedStockExchangeUrl;
            _exchangeName = exchangeName;
            _tradingPair = tradingPair;
        }

        public async Task ConnectAndReceiveAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PriceReader));

            _webSocket?.Dispose();
            ClientWebSocket webSocket = new ClientWebSocket();

            try
            {
                UriBuilder uriBuilder = new UriBuilder(_unifiedStockExchangeUrl);
                uriBuilder.Path = $"LatestPrice/{_exchangeName}/{_tradingPair.Item1}/{_tradingPair.Item2}/ws";

                _tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await webSocket.ConnectAsync(uriBuilder.Uri, _tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                throw new ApplicationException("Could not connect to UnifiedStockExchange LatestPrice WebSocket.");
            }

            _webSocket = webSocket;

            ReceivePriceUpdatesAsync();
        }

        public void Disconnect()
        {
            try
            {
                _webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed.", new CancellationTokenSource(2000).Token)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                _webSocket?.Dispose();
            }
        }

        private async void ReceivePriceUpdatesAsync()
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                string json = await ReceiveMessageAsync();
                json = json.Substring(0, json.IndexOf((char)0));
                PriceUpdateWithTimestamp priceUpdate = JsonSerializer.Deserialize<PriceUpdateWithTimestamp>(json);

                if (priceUpdate == null)
                    throw new ApplicationException("Failed to deserialize price update data.");

                PriceUpdateReceived?.Invoke(new PriceUpdateWithDate
                {
                    TradingPair = priceUpdate.TradingPair,
                    Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(priceUpdate.Time),
                    Price = priceUpdate.Price,
                    Amount = priceUpdate.Amount
                });
            }
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
            return Encoding.UTF8.GetString(new ArraySegment<byte>(memory.GetBuffer(), 0, (int)memory.Length).Array);
        }

        bool _isDisposed;
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _webSocket?.Dispose();
                _tokenSource?.Dispose();
            }
        }
    }
}