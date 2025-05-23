﻿using System;
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
        private ClientWebSocket _webSocket;

        public PriceWriter(string unifiedStockExchangeUrl, string exchangeName)
        {
            _unfiedStockExchangeUrl = unifiedStockExchangeUrl;
            _exchangeName = exchangeName;
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
                uriBuilder.Path = $"PriceIngest/{_exchangeName}/ws";

                CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await webSocket.ConnectAsync(uriBuilder.Uri, tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                throw new ApplicationException("Could not connect to UnifiedStockExchange PriceIngest WebSocket.");
            }

            _webSocket = webSocket;

            // Process WebSocket close event.
            _ = _webSocket.ReceiveAsync(new ArraySegment<byte>(new byte[10]), CancellationToken.None);
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
        public async Task SendPriceUpdateAsync(ValueTuple<string, string> tradingPair, DateTime time, decimal price, decimal amount)
        {
            if (_webSocket.State == WebSocketState.CloseReceived)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                throw new ApplicationException("UnifiedStockExchange closed WebSocket connection.");
            }

            string jsonData = JsonSerializer.Serialize(new PriceUpdateWithTimestamp
            {
                Time = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
                Price = price,
                Amount = amount,
                TradingPair = $"{tradingPair.Item2}-{tradingPair.Item1}"
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
                Disconnect();
            }
        }
    }
}
