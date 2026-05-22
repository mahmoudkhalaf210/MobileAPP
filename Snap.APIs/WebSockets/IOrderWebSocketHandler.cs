using System.Net.WebSockets;

namespace Snap.APIs.WebSockets
{
    public interface IOrderWebSocketHandler
    {
        Task HandleAsync(WebSocket socket, string connectionId, CancellationToken ct);
    }
}
