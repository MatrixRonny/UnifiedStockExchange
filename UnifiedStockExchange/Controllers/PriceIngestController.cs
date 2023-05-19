﻿using Microsoft.AspNetCore.Mvc;
using System.Net.Sockets;
using System;
using System.Net.WebSockets;
using System.Text;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Data;
using System.Dynamic;
using System.Diagnostics;
using UnifiedStockExchange.Domain.DataTransfer;
using UnifiedStockExchange.Utility;

namespace UnifiedStockExchange.Controllers
{
    public class PriceIngestController : WebSocketControllerBase
    {
        // Stock Data library in Python: https://github.com/ccxt/ccxt

        private readonly PriceExchangeService _priceService;
        private readonly PricePersistenceService _persistenceService;

        public PriceIngestController(PriceExchangeService priceService, PricePersistenceService persistenceService)
        {
            _priceService = priceService;
            _persistenceService = persistenceService;
        }

        [HttpGet("{exchangeName}/ws")]
        public async Task Get(string exchangeName)
        {
            //TODO: Adjust this WebSocket to receive multiple currencies.

            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using (WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync())
                {
                    await RecordPriceUpdates(exchangeName, webSocket);
                }
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task RecordPriceUpdates(string exchangeName, WebSocket webSocket)
        {
            Dictionary<string, PriceUpdateHandler> priceHandlers = new Dictionary<string, PriceUpdateHandler>();

            try
            {
                // https://stackoverflow.com/a/23784968/2109230

                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[256]);
                WebSocketReceiveResult? wsResult = null;

                while (wsResult?.CloseStatus == null)
                {
                    //TODO: Implement DoS protection.

                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            wsResult = await webSocket.ReceiveAsync(buffer, GetTimeoutToken(2 * 60 * 1000));
                            if (wsResult.MessageType != WebSocketMessageType.Text)
                            {
                                await SendMessageAndClose(webSocket, "Only text messages supported.");
                                return;
                            }

                            ms.Write(buffer.Array!, 0, wsResult.Count);
                        }
                        while (!wsResult.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);
                        string json = Encoding.UTF8.GetString(new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length));
                        PriceUpdateData priceUpdate = JsonSerializer.Deserialize<PriceUpdateData>(json)!;

                        try
                        {
                            DateTime time = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(priceUpdate.Time);
                            _persistenceService.RecordPrice(
                                exchangeName, 
                                priceUpdate.TradingPair.ToTradingPair(), 
                                time, 
                                priceUpdate.Price, 
                                priceUpdate.Amount
                            );

                            PriceUpdateHandler priceHandler;
                            lock(priceHandlers)
                            {
                                var tradingPair = priceUpdate.TradingPair.ToTradingPair();
                                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                                if (!priceHandlers.TryGetValue(exchangeQuote, out priceHandler!))
                                {
                                    priceHandler = priceHandlers[exchangeQuote] = _priceService.ListenForUpdates(exchangeName, tradingPair);
                                }
                            }

                            priceHandler(time, priceUpdate.Price, priceUpdate.Amount);
                        }
                        catch
                        {
                            string message = "Invalid message payload. Expecting: { \"time\": 1682104181000, \"price\": 15328.28, \"amount\": 825.84 } where time is Unix Timestamp in milliseconds.";
                            await SendMessageAndClose(webSocket, message);
                            return;
                        }
                    }
                }
            }
            finally
            {
                foreach (string exchangeQuote in priceHandlers.Keys)
                {
                    var (_, tradingPair) = exchangeQuote.ToExchangeAndTradingPair();

                    _priceService.CancelListen(exchangeName, tradingPair);
                    _persistenceService.FlushAndRemoveFromCache(exchangeName, tradingPair);
                }
            }
        }
    }
}
