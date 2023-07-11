using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.DataTransfer;
using UnifiedStockExchange.Domain.Entities;
using UnifiedStockExchange.Services;
using UnifiedStockExchange.Utility;

namespace UnifiedStockExchange.Controllers
{
    public class LatestPriceController : WebSocketControllerBase
    {
        private readonly PriceExchangeService _priceService;
        private readonly PricePersistenceService _persistenceService;

        public LatestPriceController(PriceExchangeService priceService, PricePersistenceService persistenceService)
        {
            _priceService = priceService;
            _persistenceService = persistenceService;
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

        /// <summary>
        /// Establish WebSocket connection that will be used to send price updates to the client.
        /// </summary>
        /// <param name="exchangeName">The exchange name where to retrieve data.</param>
        /// <param name="fromCurrency">The currency for the price.</param>
        /// <param name="toCurrency">The currency that for which the price is calculated.</param>
        /// <param name="fromDate">Optional: Send data starting from the specified UTC date. Returns less data than received in real-time.</param>
        [HttpGet("{exchangeName}/{fromCurrency}/{toCurrency}/ws")]
        public async Task Get(string exchangeName, string fromCurrency, string toCurrency, DateTime? fromDate = null)
        {
            if(fromDate != null && (DateTime.UtcNow - fromDate.Value).TotalDays > 30)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                using (StreamWriter writer = new StreamWriter(HttpContext.Response.Body))
                {
                    writer.Write("From date cannot be older than 30 days.");
                }
                return;
            }

            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await SendPriceUpdatesAsync(exchangeName, (fromCurrency, toCurrency), webSocket, fromDate);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task SendPriceUpdatesAsync(string exchangeName, ValueTuple<string, string> tradingPair, WebSocket webSocket, DateTime? fromDate = null)
        {
            PriceUpdateHandler priceForwarder = null!;
            try
            {
                // Send history based on fromDate before sending real-time data.

                object lockObject = new object();
                priceForwarder = async (pairName, time, price, amount) =>
                {
                    try
                    {
                        if (fromDate != null)
                        {
                            // Determine history after fromDate, but before current price time.
                            IList<PriceCandle> priceHistory = _persistenceService.SelectPriceData(exchangeName, tradingPair)
                                .Where(it => it.Date > fromDate && it.Date < time)
                                .ExecuteSelect();

                            // Disable this if.
                            fromDate = null;

                            // Send history recursively before sending the current price.
                            foreach (PriceCandle item in priceHistory)
                            {
                                await priceForwarder(pairName, item.Date, item.Open, item.Volume);
                            }
                        }

                        long unixTimeMillis = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                        string json = JsonSerializer.Serialize(new PriceUpdateData 
                        { 
                            TradingPair = pairName,
                            Time = unixTimeMillis, 
                            Price = price,
                            Amount = amount 
                        });
                        await webSocket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch(Exception e) when(e is not WebSocketException)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "An unexpected error has occurred.", GetTimeoutToken());
                        throw;
                    }
                };

                // This partial duplication of priceForwarder lambda ensures that most of the history is sent before
                // starting to listen for real-time data.
                if(fromDate != null)
                {
                    // Retrieve all available price history starting with fromDate.
                    IList<PriceCandle> priceHistory = _persistenceService.SelectPriceData(exchangeName, tradingPair)
                        .Where(it => it.Date >= fromDate)
                        .ExecuteSelect();

                    // Avoid the if at the beginning of priceForwarder.
                    fromDate = null;

                    //TODO: Optimize to use reader instead of retrieving all records.
                    foreach (PriceCandle price in priceHistory)
                    {
                        await priceForwarder(tradingPair.ToPairString(), price.Date, price.Open, price.Volume);
                    }

                    // Specify what was the last sent history item.
                    fromDate = priceHistory.Last().Date;
                }

                CancellationTokenSource tokenSource;
                lock (_activeForwarders)
                {
                    tokenSource = _activeForwarders[priceForwarder] = new CancellationTokenSource();
                }
                _priceService.AddForwardHandler(exchangeName, tradingPair, priceForwarder);

                try
                {
                    // Wait until other side closes websoket or incoming price handler is removed.
                    var receiveResult = await webSocket.ReceiveAsync(new byte[20], tokenSource.Token);

                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Not expecting to receive any messages.", GetTimeoutToken());
                }
                catch(OperationCanceledException)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Exchange and trading pair no longer available", GetTimeoutToken());
                }
            }
            catch(ApplicationException e)
            {
                // Could not add ForwardHandler.
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, e.Message, GetTimeoutToken());
            }
            catch(WebSocketException)
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
