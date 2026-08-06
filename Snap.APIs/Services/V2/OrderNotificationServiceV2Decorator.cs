using Microsoft.AspNetCore.SignalR;
using Snap.APIs.DTOs;
using Snap.APIs.Hubs;
using Snap.Business.Interfaces;
using Snap.Core.Entities;

namespace Snap.APIs.Services.V2
{
    /// <summary>
    /// Decorates the existing, untouched <see cref="OrderNotificationService"/>: every
    /// call is forwarded to it FIRST and unconditionally, so the existing FCM +
    /// WebSocket notification flow (payloads, tokens, batching — everything Flutter
    /// already consumes) keeps working exactly as before, even if the app is offline
    /// from SignalR entirely. SignalR push and points-award are purely additive,
    /// best-effort side effects layered on top: any failure in that additional work
    /// is caught and logged here, never allowed to propagate and turn an otherwise
    /// successful legacy operation into a failed HTTP response.
    ///
    /// Registered in place of the concrete OrderNotificationService in Program.cs, so
    /// every existing caller (OrderService, ScheduledRideProcessorService) gets both
    /// behaviors automatically without a single line of those files changing.
    /// </summary>
    public sealed class OrderNotificationServiceV2Decorator : IOrderNotificationService
    {
        private readonly OrderNotificationService _inner;
        private readonly IHubContext<OrdersHubV2> _hub;
        private readonly IPointsService _pointsService;
        private readonly ILogger<OrderNotificationServiceV2Decorator> _logger;

        public OrderNotificationServiceV2Decorator(
            OrderNotificationService inner,
            IHubContext<OrdersHubV2> hub,
            IPointsService pointsService,
            ILogger<OrderNotificationServiceV2Decorator> logger)
        {
            _inner = inner;
            _hub = hub;
            _pointsService = pointsService;
            _logger = logger;
        }

        public async Task NotifyOrderAcceptedAsync(int orderId, int driverId, OrderDto order)
        {
            await _inner.NotifyOrderAcceptedAsync(orderId, driverId, order);
            await BestEffort(orderId, async () =>
            {
                await PushUser(order.UserId, "ActiveOrdersUpdated");
                await PushAll();
            });
        }

        public async Task NotifyScheduledOrderAcceptedAsync(int orderId, int driverId, OrderDto order)
        {
            await _inner.NotifyScheduledOrderAcceptedAsync(orderId, driverId, order);
            await BestEffort(orderId, async () =>
            {
                await PushUser(order.UserId, "ActiveOrdersUpdated");
                await PushUser(order.UserId, "ScheduledOrdersUpdated");
                await PushAll();
            });
        }

        public async Task NotifyScheduledRideReminderAsync(int orderId, int driverId, DateTime scheduledDateUtc)
        {
            // Informational reminder only — no order-list membership changes, nothing to push.
            await _inner.NotifyScheduledRideReminderAsync(orderId, driverId, scheduledDateUtc);
        }

        public async Task NotifyScheduledRideStartingSoonAsync(int orderId, int driverId, OrderDto order)
        {
            await _inner.NotifyScheduledRideStartingSoonAsync(orderId, driverId, order);
            await BestEffort(orderId, async () =>
            {
                await PushUser(order.UserId, "ScheduledOrdersUpdated");
                await PushUser(order.UserId, "ActiveOrdersUpdated");
                await PushAll();
            });
        }

        public async Task NotifyOrderCancelledAsync(int orderId, OrderDto order)
        {
            await _inner.NotifyOrderCancelledAsync(orderId, order);
            await BestEffort(orderId, async () =>
            {
                await PushUser(order.UserId, "CancelledOrdersUpdated");
                await PushUser(order.UserId, "ActiveOrdersUpdated");
                await PushAll();
            });
        }

        public async Task NotifyWorkflowTransitionAsync(int orderId, OrderDto order, OrderStatus targetStatus)
        {
            await _inner.NotifyWorkflowTransitionAsync(orderId, order, targetStatus);
            await BestEffort(orderId, async () =>
            {
                switch (targetStatus)
                {
                    case OrderStatus.Complete:
                        await PushUser(order.UserId, "CompletedOrdersUpdated");
                        await PushUser(order.UserId, "ActiveOrdersUpdated");
                        // Additive + idempotent (see IUserPointsRepository.TryAwardPointsForOrderAsync)
                        // — never replaces or blocks the completion notification above.
                        await _pointsService.AwardForCompletedOrderAsync(orderId, order.UserId);
                        break;

                    case OrderStatus.Cancel:
                        await PushUser(order.UserId, "CancelledOrdersUpdated");
                        await PushUser(order.UserId, "ActiveOrdersUpdated");
                        break;

                    default: // Arrived / Started
                        await PushUser(order.UserId, "ActiveOrdersUpdated");
                        break;
                }

                await PushAll();
            });
        }

        public async Task NotifyDriversOfNewOrderAsync(
            OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct)
        {
            await _inner.NotifyDriversOfNewOrderAsync(order, targetDriverIds, ct);
            await BestEffort(order.Id, async () =>
            {
                await PushUser(order.UserId, "ActiveOrdersUpdated");
                await PushAll();
            });
        }

        public async Task NotifyDriversOfScheduledOrderAsync(
            OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct)
        {
            await _inner.NotifyDriversOfScheduledOrderAsync(order, targetDriverIds, ct);
            await BestEffort(order.Id, async () =>
            {
                await PushUser(order.UserId, "ScheduledOrdersUpdated");
                await PushAll();
            });
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private Task PushUser(string userId, string eventName) =>
            _hub.Clients.Group(OrdersHubV2.UserGroup(userId)).SendAsync(eventName);

        private Task PushAll() =>
            _hub.Clients.Group(OrdersHubV2.AllOrdersGroup).SendAsync("AllOrdersUpdated");

        private async Task BestEffort(int orderId, Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "v2 realtime/points post-processing failed for order {OrderId}", orderId);
            }
        }
    }
}
