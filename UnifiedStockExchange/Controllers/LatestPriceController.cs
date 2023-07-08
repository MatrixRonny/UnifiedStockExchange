using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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
            _priceService.ForwardingHandlerRemoved += OnForwardingHandlerRemoved;
        }

        Dictionary<PriceUpdateHandler, CancellationTokenSource> _activeForwarders = new Dictionary<PriceUpdateHandler, CancellationTokenSource>();
        private void OnForwardingHandlerRemoved(PriceUpdateHandler priceForwarder)
        {
            CancellationTokenSource? tokenSource;
            lock(_activeForwarders)
            {
                _activeForwarders.TryGetValue(priceForwarder, out tokenSource);
            }

            tokenSource?.Cancel();
        }

        [HttpGet("{exchangeName}/{fromCurrency}/{toCurrency}/ws")]
        public async Task Get(string exchangeName, string fromCurrency, string toCurrency)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await SendPriceUpdatesAsync(exchangeName, (fromCurrency, toCurrency), webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task SendPriceUpdatesAsync(string exchangeName, ValueTuple<string, string> tradingPair, WebSocket webSocket)
        {
            PriceUpdateHandler? priceForwarder = null;
            try
            {
                object lockObject = new object();
                priceForwarder = async (tradingPair, time, price, amount) =>
                {
                    Monitor.Enter(lockObject);
                    try
                    {
                        long unixTimeMillis = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                        string json = JsonSerializer.Serialize(new PriceUpdateData 
                        { 
                            TradingPair = tradingPair,
                            Time = unixTimeMillis, 
                            Price = price,
                            Amount = amount 
                        });
                        await webSocket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "An unexpected error has occured.", GetTimeoutToken());
                        return;
                    }
                    finally
                    {
                        Monitor.Exit(lockObject);
                    }
                };

                CancellationTokenSource tokenSource;
                lock (_activeForwarders)
                {
                    tokenSource = _activeForwarders[priceForwarder] = new CancellationTokenSource();
                }
                _priceService.AddForwardHandler(exchangeName, tradingPair, priceForwarder);

                // Wait until other side closes websoket or incoming price handler is removed.
                var receiveResult = await webSocket.ReceiveAsync(new byte[20], tokenSource.Token);

                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Not expecting to receive any messages.", GetTimeoutToken());
            }
            catch(ApplicationException e)
            {
                // Could not add ForwardHandler.
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, e.Message, GetTimeoutToken());
            }
            catch(WebSocketException e)
            {
                // Client closed WebSocket connection.
                _priceService.RemoveForwardHandler(exchangeName, tradingPair, priceForwarder!);
            }
            finally
            {
                _activeForwarders.Remove(priceForwarder!);
                webSocket.Dispose();
            }
        }
    }
}
