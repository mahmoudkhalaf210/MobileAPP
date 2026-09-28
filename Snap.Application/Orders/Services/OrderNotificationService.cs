using Snap.Application.Common.Interfaces.Notifications;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;
using Microsoft.Extensions.Logging;

namespace Snap.Application.Orders.Services
{
    /// <summary>
    /// Handles all FCM + WebSocket notifications for order lifecycle events.
    ///
    /// Registered as scoped. For background jobs (new-order dispatch) the caller
    /// resolves this service from a fresh DI scope so the injected repositories are
    /// not shared with any HTTP request.
    /// </summary>
    public sealed class OrderNotificationService : IOrderNotificationService
    {
        private const int FcmBatchSize = 20;

        private readonly IOrderNotificationRepository _notifRepo;
        private readonly INotificationService _fcm;
        private readonly IOrderWebSocketNotifier _ws;
        private readonly ILogger<OrderNotificationService> _logger;

        public OrderNotificationService(
            IOrderNotificationRepository       notifRepo,
            INotificationService               fcm,
            IOrderWebSocketNotifier            ws,
            ILogger<OrderNotificationService>  logger)
        {
            _notifRepo = notifRepo;
            _fcm     = fcm;
            _ws      = ws;
            _logger  = logger;
        }

        // ── Order accepted ────────────────────────────────────────────────────────

        public async Task NotifyOrderAcceptedAsync(int orderId, int driverId, OrderDto order)
        {
            var driverName = await _notifRepo.GetDriverNameAsync(driverId) ?? "الكابتن";

            await _ws.BroadcastOrderStatusAsync(
                new { orderId, status = "approved", driverId }, default);

            var userToken = await _notifRepo.GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
            {
                await _fcm.SendNotification(
                    userToken,
                    "تم قبول طلبك",
                    $"{driverName} قبل طلبك وفي الطريق إليك!",
                    BuildPayload("order_approved", orderId, order, driverName));
            }

            var driverToken = await _notifRepo.GetDriverFcmTokenAsync(driverId);
            if (!string.IsNullOrEmpty(driverToken))
            {
                await _fcm.SendNotification(
                    driverToken,
                    "تم قبول الطلب",
                    "لقد قبلت الطلب بنجاح.",
                    BuildPayload("order_accepted", orderId, order));
            }
        }

        public async Task NotifyScheduledOrderAcceptedAsync(int orderId, int driverId, OrderDto order)
        {
            var driverName = await _notifRepo.GetDriverNameAsync(driverId) ?? "الكابتن";

            await _ws.BroadcastOrderStatusAsync(
                new { orderId, status = "scheduled_accepted", driverId }, default);

            var userToken = await _notifRepo.GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
            {
                await _fcm.SendNotification(
                    userToken,
                    "تم قبول رحلتك المجدولة",
                    $"{driverName} حجز رحلتك المجدولة.",
                    BuildPayload("scheduled_order_accepted", orderId, order, driverName));
            }

            var driverToken = await _notifRepo.GetDriverFcmTokenAsync(driverId);
            if (!string.IsNullOrEmpty(driverToken))
            {
                await _fcm.SendNotification(
                    driverToken,
                    "تم حجز رحلة مجدولة",
                    $"لقد حجزت رحلة مجدولة الساعة {FormatEgyptTime(order.Date)}.",
                    BuildPayload("scheduled_order_reserved", orderId, order));
            }
        }

        public async Task NotifyScheduledRideReminderAsync(int orderId, int driverId, OrderDto order)
        {
            var driverToken = await _notifRepo.GetDriverFcmTokenAsync(driverId);
            if (string.IsNullOrEmpty(driverToken))
                return;

            await _fcm.SendNotification(
                driverToken,
                "تذكير برحلة مجدولة",
                $"تذكير: لديك رحلة مجدولة الساعة {FormatEgyptTime(order.Date)}.",
                BuildPayload("scheduled_reminder", orderId, order));
        }

        public async Task NotifyScheduledRideStartingSoonAsync(int orderId, int driverId, OrderDto order)
        {
            await _ws.BroadcastOrderStatusAsync(
                new { orderId, status = order.Status, driverid = order.Driverid }, default);

            var userToken = await _notifRepo.GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
            {
                await _fcm.SendNotification(
                    userToken,
                    "رحلتك ستبدأ قريباً",
                    "رحلتك المجدولة ستبدأ قريباً.",
                    BuildPayload("scheduled_starting_soon", orderId, order));
            }

            var driverToken = await _notifRepo.GetDriverFcmTokenAsync(driverId);
            if (!string.IsNullOrEmpty(driverToken))
            {
                await _fcm.SendNotification(
                    driverToken,
                    "الرحلة ستبدأ قريباً",
                    "رحلتك المجدولة ستبدأ قريباً، برجاء الاستعداد.",
                    BuildPayload("scheduled_starting_soon", orderId, order));
            }
        }

        // ── Order cancelled ───────────────────────────────────────────────────────

        public async Task NotifyOrderCancelledAsync(int orderId, OrderDto order, string? userMessage = null)
        {
            await _ws.BroadcastOrderCancelledAsync(orderId, default);

            var payload   = BuildPayload("order_cancelled", orderId, order);
            var userToken = await _notifRepo.GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;

            if (!string.IsNullOrEmpty(userToken))
                await _fcm.SendNotification(userToken, "تم إلغاء الطلب", userMessage ?? "تم إلغاء طلبك.", payload);

            if (order.Driverid.HasValue)
            {
                var driverToken = await _notifRepo.GetDriverFcmTokenAsync(order.Driverid.Value);
                if (!string.IsNullOrEmpty(driverToken))
                    await _fcm.SendNotification(driverToken, "تم إلغاء الطلب", "تم إلغاء الطلب.", payload);
            }
        }

        // ── Workflow transition ───────────────────────────────────────────────────

        public async Task NotifyWorkflowTransitionAsync(int orderId, OrderDto order, OrderStatus targetStatus)
        {
            await _ws.BroadcastOrderStatusAsync(
                new { orderId, status = order.Status, driverid = order.Driverid }, default);

            var (type, userTitle, userBody, driverTitle, driverBody) = WorkflowMessages(targetStatus);
            var payload   = BuildPayload(type, orderId, order);
            var userToken = await _notifRepo.GetUserFcmTokenAsync(order.UserId) ?? order.FCMToken;

            if (!string.IsNullOrEmpty(userToken))
                await _fcm.SendNotification(userToken, userTitle, userBody, payload);

            if (order.Driverid.HasValue)
            {
                var driverToken = await _notifRepo.GetDriverFcmTokenAsync(order.Driverid.Value);
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

                // Data path: order-list data over WebSocket, unchanged from the deployed contract.
                await BroadcastNewOrderDataAsync(order, targetDriverIds, ct);

                // Push path: FCM only. Recipients are chosen from the DB (car type + Pink Mode,
                // DriverOrderMatching) and never depend on WebSocket connection/subscription state.
                var carType = ParseCarType(order.CarType);
                var tokens = targetDriverIds is null
                    ? await _notifRepo.GetAllDriverTokensAsync(carType, order.PinkMode, ct)
                    : await _notifRepo.GetTargetDriverTokensAsync(targetDriverIds, carType, order.PinkMode, ct);

                if (tokens.Count == 0) return;

                _logger.LogInformation(
                    "Order {Id}: {Mode} — notifying {Count} token(s)",
                    order.Id,
                    targetDriverIds is null ? "AllDrivers" : "NearestOnly",
                    tokens.Count);

                await _fcm.SendBatchNotificationsAsync(
                    tokens,
                    "طلب جديد متاح",
                    "يوجد طلب رحلة جديد، افتح التطبيق الآن!",
                    BuildPayload("new_order", order.Id, order),
                    FcmBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Driver notification failed for order {Id}", order.Id);
            }
        }

        public async Task NotifyDriversOfScheduledOrderAsync(
            OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct)
        {
            try
            {
                if (targetDriverIds is { Count: 0 })
                {
                    _logger.LogWarning("Order {Id}: NearestOnly — no available drivers nearby, skipping", order.Id);
                    return;
                }

                await BroadcastNewOrderDataAsync(order, targetDriverIds, ct);

                // Push path: same FCM eligibility rules as immediate orders.
                var carType = ParseCarType(order.CarType);
                var tokens = targetDriverIds is null
                    ? await _notifRepo.GetAllDriverTokensAsync(carType, order.PinkMode, ct)
                    : await _notifRepo.GetTargetDriverTokensAsync(targetDriverIds, carType, order.PinkMode, ct);

                if (tokens.Count == 0) return;

                await _fcm.SendBatchNotificationsAsync(
                    tokens,
                    "رحلة مجدولة متاحة",
                    $"رحلة مجدولة الساعة {FormatEgyptTime(order.Date)}، افتح التطبيق لقبولها.",
                    BuildPayload("scheduled_order", order.Id, order),
                    FcmBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled driver notification failed for order {Id}", order.Id);
            }
        }

        // order.CarType is the legacy free-text field (set from the typed v2 enum's
        // ToString() for v2 orders, or arbitrary client text for v1 orders). Parses
        // cleanly for v2 traffic; v1 orders that don't match an enum name fall back
        // to null (no car-type filtering), preserving today's behavior for them.
        private static CarType? ParseCarType(string? carType) =>
            Enum.TryParse<CarType>(carType, ignoreCase: true, out var parsed) ? parsed : null;

        // WebSocket "NewOrder" is order data for list refresh, not a notification. Same
        // targeting as the deployed contract (null = every connected socket), and isolated
        // so a socket failure can never prevent the FCM push that follows.
        private async Task BroadcastNewOrderDataAsync(OrderDto order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct)
        {
            try
            {
                await _ws.BroadcastNewOrderAsync(order, targetDriverIds, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebSocket NewOrder broadcast failed for order {Id}; FCM push continues", order.Id);
            }
        }

        // ── Payload builders ──────────────────────────────────────────────────────

        // Carries the full order, not just a summary — every notification type
        // (new order, accepted, cancelled, workflow transitions, reminders, ...)
        // uses this so the client never has to fetch the order separately to show details.
        private static Dictionary<string, string> BuildPayload(
            string type, int orderId, OrderDto o, string? driverName = null)
        {
            var payload = new Dictionary<string, string>
            {
                { "type",           type                         },
                { "orderId",        orderId.ToString()           },
                { "userId",         o.UserId      ?? ""          },
                { "customerName",   o.UserName    ?? ""          },
                { "userPhone",      o.UserPhone   ?? ""          },
                { "userImage",      o.UserImage   ?? ""          },
                { "customerLat",    o.FromLatLng.Lat.ToString()  },
                { "customerLng",    o.FromLatLng.Lng.ToString()  },
                { "destinationLat", o.ToLatLng.Lat.ToString()    },
                { "destinationLng", o.ToLatLng.Lng.ToString()    },
                { "price",          o.ExpectedPrice.ToString()   },
                { "distance",       o.Distance.ToString()        },
                { "fromPlace",      o.From                       },
                { "toPlace",        o.To                         },
                { "orderType",      o.Type        ?? ""          },
                { "carType",        o.CarType     ?? ""          },
                { "pinkMode",       o.PinkMode.ToString()        },
                { "paymentWay",     o.PaymentWay  ?? ""          },
                { "noPassengers",   o.NoPassengers.ToString()    },
                { "notes",          o.Notes       ?? ""          },
                { "status",         o.Status      ?? ""          },
                { "driverId",       o.Driverid?.ToString() ?? "" },
                { "review",         o.Review.ToString()          },
                { "scheduledAtUtc", DateTime.SpecifyKind(o.Date, DateTimeKind.Utc).ToString("O") }
            };

            if (driverName != null)
                payload["driverName"] = driverName;

            return payload;
        }

        private static (string type, string userTitle, string userBody, string driverTitle, string driverBody)
            WorkflowMessages(OrderStatus status) => status switch
        {
            OrderStatus.Arrived  => ("driver_arrived", "الكابتن وصل",       "الكابتن وصل إلى مكان الانطلاق.",   "تم الوصول",      "لقد وصلت إلى مكان الانطلاق."),
            OrderStatus.Started  => ("trip_started",   "بدأت الرحلة",       "بدأت رحلتك، رحلة سعيدة!",          "بدأت الرحلة",    "تم بدء الرحلة."),
            OrderStatus.Complete => ("trip_completed",  "انتهت الرحلة",     "لقد وصلت إلى وجهتك.",               "انتهت الرحلة",   "تم إنهاء الرحلة بنجاح."),
            _                    => ("update",          "تحديث الطلب",      "تم تحديث طلبك.",                    "تحديث الطلب",    "تم تحديث الطلب.")
        };

        // Order dates are stored in UTC; users read times in Egypt local time (DST-aware).
        private static readonly TimeZoneInfo EgyptTimeZone = ResolveEgyptTimeZone();

        private static TimeZoneInfo ResolveEgyptTimeZone()
        {
            foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
                    return tz;
            }
            return TimeZoneInfo.CreateCustomTimeZone("Egypt", TimeSpan.FromHours(3), "Egypt", "Egypt");
        }

        // e.g. "07:30 م" — built by hand so it doesn't depend on the ar-EG culture being installed.
        private static string FormatEgyptTime(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), EgyptTimeZone);
            var suffix = local.Hour < 12 ? "ص" : "م";
            return $"{local.ToString("hh:mm", System.Globalization.CultureInfo.InvariantCulture)} {suffix}";
        }
    }
}
