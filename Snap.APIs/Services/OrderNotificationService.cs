using Microsoft.EntityFrameworkCore;
using Snap.APIs.DTOs;
using Snap.APIs.WebSockets;
using Snap.Core.Entities;
using Snap.Core.Services;
using Snap.Repository.Data;

namespace Snap.APIs.Services
{
    /// <summary>
    /// Handles all FCM + WebSocket notifications for order lifecycle events.
    ///
    /// Registered as scoped. For background jobs (new-order dispatch) the caller
    /// resolves this service from a fresh DI scope so the injected DbContext is
    /// not shared with any HTTP request.
    /// </summary>
    public sealed class OrderNotificationService : IOrderNotificationService
    {
        private const int FcmBatchSize = 20;

        private readonly SnapDbContext        _context;
        private readonly INotificationService _fcm;
        private readonly IWebSocketHub        _hub;
        private readonly ILogger<OrderNotificationService> _logger;

        public OrderNotificationService(
            SnapDbContext                       context,
            INotificationService               fcm,
            IWebSocketHub                      hub,
            ILogger<OrderNotificationService>  logger)
        {
            _context = context;
            _fcm     = fcm;
            _hub     = hub;
            _logger  = logger;
        }

        // ── Order accepted ────────────────────────────────────────────────────────

        public async Task NotifyOrderAcceptedAsync(int orderId, int driverId, OrderDto order)
        {
            var driverName = await _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Select(d => d.DriverFullname)
                .FirstOrDefaultAsync() ?? "A driver";

            await _hub.BroadcastOrderStatusAsync(
                new { orderId, status = "approved", driverId }, default);

            var userToken = await GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
            {
                await _fcm.SendNotification(
                    userToken,
                    "Order Accepted",
                    $"{driverName} has accepted your order!",
                    BuildPayload("order_approved", orderId, order, driverName));
            }

            var driverToken = await GetDriverFcmTokenAsync(driverId);
            if (!string.IsNullOrEmpty(driverToken))
            {
                await _fcm.SendNotification(
                    driverToken,
                    "Order Accepted",
                    "You have successfully accepted the order.",
                    BuildPayload("order_accepted", orderId, order));
            }
        }

        // ── Order cancelled ───────────────────────────────────────────────────────

        public async Task NotifyOrderCancelledAsync(int orderId, OrderDto order)
        {
            await _hub.BroadcastOrderCancelledAsync(orderId, default);

            var payload   = BuildPayload("order_cancelled", orderId, order);
            var userToken = await GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;

            if (!string.IsNullOrEmpty(userToken))
                await _fcm.SendNotification(userToken, "Order Cancelled", "Your order has been cancelled.", payload);

            if (order.Driverid.HasValue)
            {
                var driverToken = await GetDriverFcmTokenAsync(order.Driverid.Value);
                if (!string.IsNullOrEmpty(driverToken))
                    await _fcm.SendNotification(driverToken, "Order Cancelled", "The order has been cancelled.", payload);
            }
        }

        // ── Workflow transition ───────────────────────────────────────────────────

        public async Task NotifyWorkflowTransitionAsync(int orderId, OrderDto order, OrderStatus targetStatus)
        {
            await _hub.BroadcastOrderStatusAsync(
                new { orderId, status = order.Status, driverid = order.Driverid }, default);

            var (type, userTitle, userBody, driverTitle, driverBody) = WorkflowMessages(targetStatus);
            var payload   = BuildPayload(type, orderId, order);
            var userToken = await GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;

            if (!string.IsNullOrEmpty(userToken))
                await _fcm.SendNotification(userToken, userTitle, userBody, payload);

            if (order.Driverid.HasValue)
            {
                var driverToken = await GetDriverFcmTokenAsync(order.Driverid.Value);
                if (!string.IsNullOrEmpty(driverToken))
                    await _fcm.SendNotification(driverToken, driverTitle, driverBody, payload);
            }
        }

        // ── New-order driver notification (background job) ────────────────────────

        public async Task NotifyDriversOfNewOrderAsync(
            OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct)
        {
            try
            {
                if (targetDriverIds is { Count: 0 })
                {
                    _logger.LogWarning("Order {Id}: NearestOnly — no available drivers nearby, skipping", order.Id);
                    return;
                }

                // WebSocket broadcast first (non-blocking for FCM)
                await _hub.BroadcastNewOrderAsync(order, targetDriverIds, ct);

                // Fetch FCM tokens
                var tokens = targetDriverIds is null
                    ? await AllDriverTokensAsync(ct)
                    : await TargetDriverTokensAsync(targetDriverIds, ct);

                if (tokens.Count == 0) return;

                _logger.LogInformation(
                    "Order {Id}: {Mode} — notifying {Count} token(s)",
                    order.Id,
                    targetDriverIds is null ? "AllDrivers" : "NearestOnly",
                    tokens.Count);

                await _fcm.SendBatchNotificationsAsync(
                    tokens,
                    "New Order Available",
                    "Check the app for a new trip request!",
                    BuildPayload("new_order", order.Id, order),
                    FcmBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Driver notification failed for order {Id}", order.Id);
            }
        }

        // ── Token helpers ─────────────────────────────────────────────────────────

        private async Task<List<string>> AllDriverTokensAsync(CancellationToken ct) =>
            await _context.Drivers
                .AsNoTracking()
                .Join(_context.FCMTokenUsers,
                      d   => d.UserId,
                      fcm => fcm.UserId,
                      (_,  fcm) => fcm.Token)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToListAsync(ct);

        private async Task<List<string>> TargetDriverTokensAsync(
            IReadOnlyList<int> driverIds, CancellationToken ct) =>
            await _context.Drivers
                .AsNoTracking()
                .Where(d => driverIds.Contains(d.Id))
                .Join(_context.FCMTokenUsers,
                      d   => d.UserId,
                      fcm => fcm.UserId,
                      (_,  fcm) => fcm.Token)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToListAsync(ct);

        private async Task<string?> GetUserFcmTokenAsync(string userId) =>
            await _context.FCMTokenUsers
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .FirstOrDefaultAsync();

        private async Task<string?> GetDriverFcmTokenAsync(int driverId) =>
            await _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Join(_context.FCMTokenUsers,
                      d => d.UserId,
                      t => t.UserId,
                      (_, t) => t.Token)
                .FirstOrDefaultAsync();

        // ── Payload builders ──────────────────────────────────────────────────────

        private static Dictionary<string, string> BuildPayload(
            string type, int orderId, OrderDto o, string? driverName = null)
        {
            var payload = new Dictionary<string, string>
            {
                { "type",           type                         },
                { "orderId",        orderId.ToString()           },
                { "customerName",   o.UserName    ?? ""          },
                { "userPhone",      o.UserPhone   ?? ""          },
                { "customerLat",    o.FromLatLng.Lat.ToString()  },
                { "customerLng",    o.FromLatLng.Lng.ToString()  },
                { "destinationLat", o.ToLatLng.Lat.ToString()    },
                { "destinationLng", o.ToLatLng.Lng.ToString()    },
                { "price",          o.ExpectedPrice.ToString()   },
                { "fromPlace",      o.From                       },
                { "toPlace",        o.To                         }
            };

            if (driverName != null)
                payload["driverName"] = driverName;

            return payload;
        }

        private static (string type, string userTitle, string userBody, string driverTitle, string driverBody)
            WorkflowMessages(OrderStatus status) => status switch
        {
            OrderStatus.Arrived  => ("driver_arrived", "Driver Arrived",   "Your driver has arrived at the pickup location.", "Arrived",        "You have arrived at the pickup location."),
            OrderStatus.Started  => ("trip_started",   "Trip Started",     "Your trip has started. Enjoy the ride!",          "Trip Started",   "The trip has started."),
            OrderStatus.Complete => ("trip_completed",  "Trip Completed",  "You have arrived at your destination.",           "Trip Completed", "The trip has been completed."),
            _                    => ("update",          "Order Updated",   "Order updated.",                                  "Order Updated",  "Order updated.")
        };
    }
}
