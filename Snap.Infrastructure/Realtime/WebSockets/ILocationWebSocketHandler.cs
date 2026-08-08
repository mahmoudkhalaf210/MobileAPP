using System.Net.WebSockets;

namespace Snap.Infrastructure.Realtime.WebSockets
{
    public interface ILocationWebSocketHandler
    {
        Task HandleAsync(WebSocket socket, string connectionId, CancellationToken ct);
    }
}
