using System.Collections.Generic;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;

namespace Snap.Infrastructure.Realtime.WebSockets
{
    // Thin Infrastructure-side implementation of the Application-owned
    // IOrderWebSocketNotifier abstraction — forwards to the transport-level IWebSocketHub.
    public sealed class OrderWebSocketNotifier : IOrderWebSocketNotifier
    {
        private readonly IWebSocketHub _hub;

        public OrderWebSocketNotifier(IWebSocketHub hub)
        {
            _hub = hub;
        }

        public Task BroadcastOrderStatusAsync(object data, CancellationToken ct = default) =>
            _hub.BroadcastOrderStatusAsync(data, ct);

        public Task BroadcastNewOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct = default) =>
            _hub.BroadcastNewOrderAsync(order, targetDriverIds, ct);

        public Task BroadcastOrderCancelledAsync(int orderId, CancellationToken ct = default) =>
            _hub.BroadcastOrderCancelledAsync(orderId, ct);
    }
}
