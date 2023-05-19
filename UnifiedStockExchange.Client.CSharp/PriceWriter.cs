using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnifiedStockExchange.Client.CSharp;

namespace UnifiedStockExchange.CSharp
{
    public class PriceWriter : IDisposable
    {
        private readonly string _unfiedStockExchangeUrl;
        private readonly string _exchangeName;
        private readonly (string, string) _tradingPair;
        private ClientWebSocket _webSocket;

        public PriceWriter(string unfiedStockExchangeUrl, string exchangeName, ValueTuple<string, string> tradingPair)
        {
            _unfiedStockExchangeUrl = unfiedStockExchangeUrl;
            _exchangeName = exchangeName;
            _tradingPair = tradingPair;
        }

        public async Task ConnectAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PriceWriter));

            _webSocket?.Dispose();
            ClientWebSocket webSocket = new ClientWebSocket();

            try
            {
                UriBuilder uriBuilder = new UriBuilder(_unfiedStockExchangeUrl);
                uriBuilder.Path = $"PriceIngest/{_exchangeName}/{_tradingPair.Item1}/{_tradingPair.Item2}/ws";

                CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await webSocket.ConnectAsync(uriBuilder.Uri, tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                throw new ApplicationException("Could not connect to UnifiedStockExchange PriceIngest WebSocket.");
            }

            _webSocket = webSocket;
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

        /// <summary>
        /// Send settlement price of the corresponding latest transaction to UnifiedStockExchange.
        /// </summary>
        /// <param name="time">The UTC time when the transaction has completed.</param>
        /// <param name="price">The settlement price of the transaction.</param>
        /// <param name="amount">The amount of currency that has been traded</param>
        /// <returns></returns>
        public async Task SendPriceUpdateAsync(DateTime time, decimal price, decimal amount)
        {
            string jsonData = JsonSerializer.Serialize(new PriceUpdateData
            {
                Time = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
                Price = price,
                Amount = amount
            });

            ArraySegment<byte> bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonData));
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, new CancellationTokenSource(2000).Token);
        }

        bool _isDisposed;
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _webSocket?.Dispose();
            }
        }
    }
}
