using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.DataTransfer;
using UnifiedStockExchange.Services;

namespace UnifiedStockExchange.Controllers
{
    public class LatestPriceController : WebSocketControllerBase
    {
        private readonly PriceExchangeService _priceService;

        public LatestPriceController(PriceExchangeService priceService)
        {
            _priceService = priceService;
        }

        [HttpGet("{exchangeName}/{fromCurency}/{toCurrency}/ws")]
        public async Task Get(string exchangeName, string fromCurrency, string toCurrency)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await SendPriceUpdates(exchangeName, (fromCurrency, toCurrency), webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task SendPriceUpdates(string exchangeName, ValueTuple<string, string> tradingPair, WebSocket webSocket)
        {
            PriceUpdateHandler? priceListener = null;
            try
            {
                object lockObject = new object();
                priceListener = async (time, price, amount) =>
                {
                    Monitor.Enter(lockObject);
                    try
                    {
                        Monitor.Wait(lockObject);

                        long unixTimeMillis = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                        string json = JsonSerializer.Serialize(new PriceUpdateData 
                        { 
                            Time = unixTimeMillis, 
                            Price = price,
                            Amount = amount 
                        });
                        await webSocket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

                        Monitor.Pulse(lockObject);
                    }
                    catch
                    {
                        await SendMessageAndClose(webSocket, "An unexpected error has occured.");
                        return;
                    }
                    finally
                    {
                        Monitor.Exit(lockObject);
                    }
                };
                _priceService.RegisterListener(exchangeName, tradingPair, priceListener);

                // Wait until other side closes websoket.
                var receiveResult = await webSocket.ReceiveAsync(new byte[20], CancellationToken.None);
            }
            finally
            {
                if (priceListener != null)
                {
                    _priceService.RegisterListener(exchangeName, tradingPair, priceListener);
                }
            }
        }
    }
}
