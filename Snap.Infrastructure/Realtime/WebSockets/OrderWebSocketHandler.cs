using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Snap.Infrastructure.Realtime.WebSockets
{
    /// <summary>
    /// Handles all /ws/orders connections.
    /// Drivers subscribe with their driverId; the hub then targets them
    /// for new-order and status-change broadcasts.
    /// </summary>
    public sealed class OrderWebSocketHandler : IOrderWebSocketHandler
    {
        private readonly IWebSocketHub _hub;
        private readonly ILogger<OrderWebSocketHandler> _logger;

        public OrderWebSocketHandler(IWebSocketHub hub, ILogger<OrderWebSocketHandler> logger)
        {
            _hub    = hub;
            _logger = logger;
        }

        public async Task HandleAsync(WebSocket socket, string connectionId, CancellationToken ct)
        {
            _hub.RegisterOrderSocket(connectionId, socket);
            var buffer = new byte[1024];

            try
            {
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", ct);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessageAsync(socket, connectionId, text, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                _logger.LogDebug("Order WS closed unexpectedly: {Msg}", ex.Message);
            }
            finally
            {
                _hub.UnregisterOrderSocket(connectionId);
            }
        }

        private async Task ProcessMessageAsync(
            WebSocket socket, string connectionId, string text, CancellationToken ct)
        {
            try
            {
                using var doc    = JsonDocument.Parse(text);
                var       action = doc.RootElement.GetProperty("action").GetString();

                switch (action)
                {
                    case "Subscribe":
                        var driverId = doc.RootElement.GetProperty("driverId").GetInt32();
                        _hub.BindOrderSocketToDriver(connectionId, driverId);
                        await _hub.SendToSocketAsync(
                            socket, new { action = "Subscribed", data = new { driverId } }, ct);
                        break;

                    case "Ping":
                        await _hub.SendToSocketAsync(
                            socket, new { action = "Pong", data = new { timestamp = DateTime.UtcNow } }, ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing order WS message");
                try
                {
                    await _hub.SendToSocketAsync(
                        socket, new { action = "Error", data = new { message = ex.Message } }, ct);
                }
                catch { }
            }
        }
    }
}
