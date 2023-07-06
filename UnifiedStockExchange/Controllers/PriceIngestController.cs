using Microsoft.AspNetCore.Mvc;
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

        private readonly PriceExchangeService _exchangeService;
        private readonly PricePersistenceService _persistenceService;

        public PriceIngestController(PriceExchangeService exchangeService, PricePersistenceService persistenceService)
        {
            _exchangeService = exchangeService;
            _persistenceService = persistenceService;
        }

        [HttpGet("{exchangeName}/ws")]
        public async Task<ActionResult> Get(string exchangeName)
        {
            if (_exchangeService.ActiveExchangeQuotes.Keys.Contains(exchangeName))
                return Conflict("There is another WebSocket sending price data for the same exchange.");

            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using (WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync())
                {
                    await RecordPriceUpdatesAsync(exchangeName, webSocket);
                    return Ok();
                }
            }
            else
            {
                return BadRequest("Expecting a WebSocker request.");
            }
        }

        private async Task RecordPriceUpdatesAsync(string exchangeName, WebSocket webSocket)
        {
            Dictionary<string, PriceUpdateHandler> priceListenerList = new Dictionary<string, PriceUpdateHandler>();

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
                            DateTime time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(priceUpdate.Time);
                            _persistenceService.RecordPrice(
                                exchangeName, 
                                priceUpdate.TradingPair.ToTradingPair(), 
                                time, 
                                priceUpdate.Price, 
                                priceUpdate.Amount
                            );

                            PriceUpdateHandler priceListener;
                            lock(priceListenerList)
                            {
                                var tradingPair = priceUpdate.TradingPair.ToTradingPair();
                                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                                if (!priceListenerList.TryGetValue(exchangeQuote, out priceListener!))
                                {
                                    priceListener = priceListenerList[exchangeQuote] = _exchangeService.CreateIncomingListener(exchangeName, tradingPair);
                                }
                            }

                            priceListener(priceUpdate.TradingPair, time, priceUpdate.Price, priceUpdate.Amount);
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
                foreach (string exchangeQuote in priceListenerList.Keys)
                {
                    var (_, tradingPair) = exchangeQuote.ToExchangeAndTradingPair();

                    _exchangeService.RemoveIncomingListener(exchangeName, tradingPair);
                    _persistenceService.FlushAndRemoveFromCache(exchangeName, tradingPair);
                }

                webSocket.Dispose();
            }
        }
    }
}
