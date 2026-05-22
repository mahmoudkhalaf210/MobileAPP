using Snap.APIs.DTOs;
using Snap.Core.Entities;

namespace Snap.APIs.Services
{
    public interface IOrderNotificationService
    {
        Task NotifyOrderAcceptedAsync(int orderId, int driverId, OrderDto order);
        Task NotifyOrderCancelledAsync(int orderId, OrderDto order);
        Task NotifyWorkflowTransitionAsync(int orderId, OrderDto order, OrderStatus targetStatus);

        /// <summary>
        /// Sends FCM batch notifications and WebSocket broadcast to drivers for a new order.
        /// Designed to run inside a background job scope (creates no ambient state).
        /// <paramref name="targetDriverIds"/> = null → notify all drivers.
        /// <paramref name="targetDriverIds"/> = list → notify only those driver IDs.
        /// </summary>
        Task NotifyDriversOfNewOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct);
    }
}
