using System.Net.WebSockets;
using Snap.Infrastructure.Realtime.WebSockets;

namespace Snap.API.Middlewares
{
    /// <summary>
    /// Routes WebSocket upgrade requests to the appropriate handler.
    /// All connection state and broadcasting logic lives in IWebSocketHub;
    /// all protocol logic lives in the handler classes.
    /// This class only decides which handler owns a given path.
    /// </summary>
    public sealed class WebSocketMiddleware
    {
        private readonly RequestDelegate          _next;
        private readonly ILocationWebSocketHandler _locationHandler;
        private readonly IOrderWebSocketHandler   _orderHandler;

        public WebSocketMiddleware(
            RequestDelegate           next,
            ILocationWebSocketHandler locationHandler,
            IOrderWebSocketHandler    orderHandler)
        {
            _next            = next;
            _locationHandler = locationHandler;
            _orderHandler    = orderHandler;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next(context);
                return;
            }

            var connectionId = Guid.NewGuid().ToString("N");

            if (context.Request.Path == "/ws/location")
            {
                var socket = await context.WebSockets.AcceptWebSocketAsync();
                await _locationHandler.HandleAsync(socket, connectionId, context.RequestAborted);
            }
            else if (context.Request.Path == "/ws/orders")
            {
                var socket = await context.WebSockets.AcceptWebSocketAsync();
                await _orderHandler.HandleAsync(socket, connectionId, context.RequestAborted);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Unknown WebSocket endpoint");
            }
        }
    }
}
