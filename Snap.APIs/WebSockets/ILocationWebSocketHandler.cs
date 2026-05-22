using System.Net.WebSockets;

namespace Snap.APIs.WebSockets
{
    public interface ILocationWebSocketHandler
    {
        Task HandleAsync(WebSocket socket, string connectionId, CancellationToken ct);
    }
}
