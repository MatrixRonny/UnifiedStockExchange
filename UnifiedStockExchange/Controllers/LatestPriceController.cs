using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Services;

namespace UnifiedStockExchange.Controllers
{
    [Route("[controller]")]
    public class LatestPriceController : ControllerBase
    {
        // Stock Data library in Python: https://github.com/ccxt/ccxt
        
        private readonly PriceExchangeService _priceService;

        public LatestPriceController(PriceExchangeService priceService)
        {
            _priceService = priceService;
        }

        [Route("{exchange}/{quote}/ws")]
        public async Task Get(string exchange, string quote)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await SendPriceUpdates(exchange, quote, webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task SendPriceUpdates(string exchange, string quote, WebSocket webSocket)
        {
            PriceUpdate? priceListener = null;
            try
            {
                object lockObject = new object();
                priceListener = async (time, price, amount) =>
                {
                    Monitor.Enter(lockObject);
                    try
                    {
                        Monitor.Wait(lockObject);

                        string json = JsonSerializer.Serialize(new { time, price, amount });
                        await webSocket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

                        Monitor.Pulse(lockObject);
                    }
                    finally
                    {
                        Monitor.Exit(lockObject);
                    }
                };
                _priceService.RegisterListener("CoinMarketCap", "BTC-USDT", priceListener);

                //INFO: receiveResult.CloseStatus != null;
                var receiveResult = await webSocket.ReceiveAsync(new byte[20], CancellationToken.None);
            }
            finally
            {
                if (priceListener != null)
                {
                    _priceService.RegisterListener("CoinMarketCap", "BTC-USDT", priceListener);
                }
            }
        }
    }
}
