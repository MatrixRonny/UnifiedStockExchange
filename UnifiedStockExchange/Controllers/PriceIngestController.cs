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

namespace UnifiedStockExchange.Controllers
{
    public class PriceIngestController : WebSocketControllerBase
    {
        // Stock Data library in Python: https://github.com/ccxt/ccxt

        private readonly PriceExchangeService _priceService;

        public PriceIngestController(PriceExchangeService priceService)
        {
            _priceService = priceService;
        }

        [Route("{exchange}/{quote}/ws")]
        public async Task Get(string exchange, string quote)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await RecordPriceUpdates(exchange, quote, webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task RecordPriceUpdates(string exchange, string quote, WebSocket webSocket)
        {
            PriceUpdate? priceHandler = _priceService.ListenForUpdates(exchange, quote);
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
                        }
                        while (!wsResult.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);
                        string json = Encoding.UTF8.GetString(ms.GetBuffer());
                        dynamic jsonObject = JsonSerializer.Deserialize<JsonObject>(json)!;

                        try
                        {
                            long unixTimeMillis = jsonObject.time;
                            DateTime time = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(unixTimeMillis);
                            decimal price = jsonObject.price;
                            decimal amount = jsonObject.amount;

                            priceHandler(time, price, amount);
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
                if (priceHandler != null)
                {
                    _priceService.RegisterListener(exchange, quote, priceHandler);
                }
            }
        }
    }
}
