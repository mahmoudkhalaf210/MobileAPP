using System;
using System.Collections.Generic;
using System.Threading;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.DTOs;

namespace Snap.Application.Orders.Interfaces
{
    public interface IOrderNotificationService
    {
        Task NotifyOrderAcceptedAsync(int orderId, int driverId, OrderDto order);
        Task NotifyScheduledOrderAcceptedAsync(int orderId, int driverId, OrderDto order);
        Task NotifyScheduledRideReminderAsync(int orderId, int driverId, OrderDto order);
        Task NotifyScheduledRideStartingSoonAsync(int orderId, int driverId, OrderDto order);
        // userMessage overrides the default user-facing body (e.g. "no driver found" for auto-cancels).
        Task NotifyOrderCancelledAsync(int orderId, OrderDto order, string? userMessage = null);
        Task NotifyWorkflowTransitionAsync(int orderId, OrderDto order, OrderStatus targetStatus);
        Task NotifyDriversOfNewOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct);
        Task NotifyDriversOfScheduledOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct);
    }
}
