using System.Net.WebSockets;

namespace Snap.Infrastructure.Realtime.WebSockets
{
    public interface IOrderWebSocketHandler
    {
        Task HandleAsync(WebSocket socket, string connectionId, CancellationToken ct);
    }
}
