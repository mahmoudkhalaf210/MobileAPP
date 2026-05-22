using System.Net.WebSockets;
using Snap.APIs.DTOs;

namespace Snap.APIs.WebSockets
{
    public interface IWebSocketHub
    {
        // ── Location channel ─────────────────────────────────────────────────────
        void RegisterLocationSocket(string connectionId, WebSocket socket);
        void UnregisterLocationSocket(string connectionId, out int? driverId);
        void BindLocationSocketToDriver(string connectionId, int driverId);
        bool TryGetLocationDriver(string connectionId, out int driverId);

        // ── Order channel ─────────────────────────────────────────────────────────
        void RegisterOrderSocket(string connectionId, WebSocket socket);
        void UnregisterOrderSocket(string connectionId);
        void BindOrderSocketToDriver(string connectionId, int driverId);

        // ── Direct send ───────────────────────────────────────────────────────────
        Task SendToSocketAsync(WebSocket socket, object message, CancellationToken ct = default);

        // ── Broadcasts ────────────────────────────────────────────────────────────

        /// <summary>Broadcasts to all /ws/location connections (clients watching the map).</summary>
        Task BroadcastLocationEventAsync(string action, object data, CancellationToken ct = default);

        /// <summary>
        /// Broadcasts a new order to drivers on /ws/orders.
        /// <paramref name="targetDriverIds"/> = null → all connected drivers.
        /// <paramref name="targetDriverIds"/> = non-null → only those driver IDs.
        /// </summary>
        Task BroadcastNewOrderAsync(object order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct = default);

        Task BroadcastOrderStatusAsync(object data, CancellationToken ct = default);
        Task BroadcastOrderCancelledAsync(int orderId, CancellationToken ct = default);
    }
}
