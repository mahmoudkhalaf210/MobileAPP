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
        Task NotifyScheduledRideReminderAsync(int orderId, int driverId, DateTime scheduledDateUtc);
        Task NotifyScheduledRideStartingSoonAsync(int orderId, int driverId, OrderDto order);
        Task NotifyOrderCancelledAsync(int orderId, OrderDto order);
        Task NotifyWorkflowTransitionAsync(int orderId, OrderDto order, OrderStatus targetStatus);
        Task NotifyDriversOfNewOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct);
        Task NotifyDriversOfScheduledOrderAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct);
    }
}
