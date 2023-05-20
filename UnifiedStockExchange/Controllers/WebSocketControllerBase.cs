﻿using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;

namespace UnifiedStockExchange.Controllers
{
    [Route("[controller]")]
    public class WebSocketControllerBase : ControllerBase
    {
        protected async Task SendMessageAndClose(WebSocket webSocket, string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, GetTimeoutToken());
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, message, new CancellationTokenSource(2000).Token);
        }

        protected CancellationToken GetTimeoutToken(int milliseconds = 1000)
        {
            return new CancellationTokenSource(milliseconds).Token;
        }
    }
}
