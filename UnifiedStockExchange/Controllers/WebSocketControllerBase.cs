using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;

namespace UnifiedStockExchange.Controllers
{
    [Route("[controller]")]
    public class WebSocketControllerBase : ControllerBase
    {
        protected CancellationToken GetTimeoutToken(int milliseconds = 1000)
        {
            return new CancellationTokenSource(milliseconds).Token;
        }
    }
}
