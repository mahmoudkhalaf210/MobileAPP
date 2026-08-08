using System.Collections.Generic;
using Snap.Application.Orders.DTOs;

namespace Snap.Application.Orders.Interfaces
{
    // Application-side abstraction over the native /ws/orders WebSocket broadcast.
    // Mirrors the three order-related members of the transport-level IWebSocketHub
    // (which stays entirely inside Snap.Infrastructure, since it carries raw
    // System.Net.WebSockets types) so OrderNotificationService never sees a socket.
    public interface IOrderWebSocketNotifier
    {
        Task BroadcastOrderStatusAsync(object data, CancellationToken ct = default);
        Task BroadcastNewOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct = default);
        Task BroadcastOrderCancelledAsync(int orderId, CancellationToken ct = default);
    }
}
