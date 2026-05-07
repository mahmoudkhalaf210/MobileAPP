using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Snap.APIs.DTOs;
using Snap.APIs.Middlewares;
using Snap.APIs.Settings;
using Snap.Core.Entities;
using Snap.Core.Services;
using Snap.Repository.Data;

namespace Snap.APIs.Services
{
    public sealed class OrderService : IOrderService
    {
        private const int NotificationBatchSize = 20;
        private const double EarthRadiusKm      = 6371.0;

        private readonly SnapDbContext           _context;
        private readonly INotificationService    _notificationService;
        private readonly IDriverLocationService  _locationService;
        private readonly IServiceScopeFactory    _scopeFactory;
        private readonly IBackgroundJobQueue     _jobQueue;
        private readonly ILogger<OrderService>   _logger;
        private readonly IOptions<OrderSettings> _options;

        public OrderService(
            SnapDbContext            context,
            INotificationService     notificationService,
            IDriverLocationService   locationService,
            IServiceScopeFactory     scopeFactory,
            IBackgroundJobQueue      jobQueue,
            ILogger<OrderService>    logger,
            IOptions<OrderSettings>  options)
        {
            _context             = context;
            _notificationService = notificationService;
            _locationService     = locationService;
            _scopeFactory        = scopeFactory;
            _jobQueue            = jobQueue;
            _logger              = logger;
            _options             = options;
        }

        // ── Create ────────────────────────────────────────────────────────────────

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Type))
                throw new ArgumentException("Order type is required.");

            var userInfo = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == dto.UserId)
                .Select(u => new { u.Image, u.FullName, u.PhoneNumber })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var order = new Order
            {
                UserId        = dto.UserId,
                Date          = dto.Date,
                From          = dto.From,
                To            = dto.To,
                FromLatLng    = new LatLng { Lat = dto.FromLatLng.Lat, Lng = dto.FromLatLng.Lng },
                ToLatLng      = new LatLng { Lat = dto.ToLatLng.Lat,   Lng = dto.ToLatLng.Lng   },
                ExpectedPrice = dto.ExpectedPrice,
                Type          = dto.Type.ToLower(),
                Distance      = dto.Distance,
                Notes         = dto.Notes,
                NoPassengers  = dto.NoPassengers,
                UserImage     = userInfo.Image,
                UserName      = userInfo.FullName,
                UserPhone     = userInfo.PhoneNumber,
                Status        = OrderStatus.Pending.GetStringValue(),
                PaymentWay    = dto.PaymentWay,
                CarType       = dto.CarType,
                PinkMode      = dto.PinkMode,
                FCMToken      = dto.FCMToken
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var settings = _options.Value;
            var orderDto = MapToDto(order);

            // targetDriverIds == null  →  AllDrivers mode (broadcast to everyone)
            // targetDriverIds != null  →  NearestOnly mode (targeted list, may be empty)
            List<int>? targetDriverIds = settings.NotificationMode == DriverNotificationMode.NearestOnly
                ? GetNearestDriverIds(dto.FromLatLng.Lat, dto.FromLatLng.Lng, settings.NearestDriverCount)
                : null;

            // Enqueue notification; HTTP response returns immediately.
            // Lambda must not capture scoped services (_context, _notificationService).
            await _jobQueue.EnqueueAsync(ct => NotifyDriversAsync(orderDto, targetDriverIds, ct));

            return orderDto;
        }

        // ── Accept (atomic, race-condition-safe) ──────────────────────────────────

        public async Task AcceptOrderAsync(UpdateOrderDriverDto dto)
        {
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET Driverid = {0}, Status = {1} WHERE Id = {2} AND Status = {3}",
                dto.Driverid,
                OrderStatus.Approved.GetStringValue(),
                dto.OrderId,
                OrderStatus.Pending.GetStringValue());

            if (rowsAffected == 0)
                throw new InvalidOperationException("Order is no longer available or already accepted.");

            // Mark driver as unavailable so future orders skip them
            _locationService.SetDriverAvailability(dto.Driverid, isAvailable: false);

            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == dto.OrderId)
                .Select(o => new OrderSnapshot
                {
                    UserId        = o.UserId,
                    FCMToken      = o.FCMToken,
                    UserName      = o.UserName,
                    UserPhone     = o.UserPhone,
                    ExpectedPrice = o.ExpectedPrice,
                    From          = o.From,
                    To            = o.To,
                    FromLat       = o.FromLatLng.Lat,
                    FromLng       = o.FromLatLng.Lng,
                    ToLat         = o.ToLatLng.Lat,
                    ToLng         = o.ToLatLng.Lng
                })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Order not found after atomic update.");

            var driverName = await _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == dto.Driverid)
                .Select(d => d.DriverFullname)
                .FirstOrDefaultAsync() ?? "A driver";

            var userToken = await GetUserTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
            {
                await _notificationService.SendNotification(
                    userToken,
                    "Order Accepted",
                    $"{driverName} has accepted your order!",
                    new Dictionary<string, string>
                    {
                        { "type",           "order_approved"               },
                        { "orderId",        dto.OrderId.ToString()         },
                        { "driverName",     driverName                     },
                        { "customerLat",    order.FromLat.ToString()       },
                        { "customerLng",    order.FromLng.ToString()       },
                        { "destinationLat", order.ToLat.ToString()         },
                        { "destinationLng", order.ToLng.ToString()         },
                        { "price",          order.ExpectedPrice.ToString() },
                        { "fromPlace",      order.From                     },
                        { "toPlace",        order.To                       }
                    });
            }

            _ = WebSocketMiddleware.BroadcastOrderStatusUpdate(
                new { orderId = dto.OrderId, status = "approved", driverid = dto.Driverid });

            var driverToken = await GetDriverTokenByIdAsync(dto.Driverid);
            if (!string.IsNullOrEmpty(driverToken))
            {
                await _notificationService.SendNotification(
                    driverToken,
                    "Order Accepted",
                    "You have successfully accepted the order.",
                    BuildPayload("order_accepted", dto.OrderId, order));
            }
        }

        // ── Driver cancels ────────────────────────────────────────────────────────

        public async Task CancelOrderByDriverAsync(UpdateOrderDriverDto dto)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            order.Status = OrderStatus.Cancel.GetStringValue();
            await _context.SaveChangesAsync();

            // Driver is free to take new orders again
            _locationService.SetDriverAvailability(dto.Driverid, isAvailable: true);

            _ = WebSocketMiddleware.BroadcastOrderCancelled(order.Id);

            var snap    = SnapshotOf(order);
            var payload = BuildPayload("order_cancelled", order.Id, snap);

            var userToken = await GetUserTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
                await _notificationService.SendNotification(userToken, "Order Cancelled", "Your order has been cancelled.", payload);

            if (order.Driverid.HasValue)
            {
                var driverToken = await GetDriverTokenByIdAsync(order.Driverid.Value);
                if (!string.IsNullOrEmpty(driverToken))
                    await _notificationService.SendNotification(driverToken, "Order Cancelled", "The order has been cancelled.", payload);
            }
        }

        // ── User cancels ──────────────────────────────────────────────────────────

        public async Task CancelOrderByUserAsync(CancelOrderByUserDto dto)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            if (order.UserId != dto.UserId)
                throw new UnauthorizedAccessException("You are not allowed to cancel this order.");

            var current = OrderStatusExtensions.FromString(order.Status ?? OrderStatus.Pending.GetStringValue());

            if (current == OrderStatus.Complete)
                throw new InvalidOperationException("You cannot cancel a completed order.");

            if (current == OrderStatus.Cancel)
                return; // already cancelled — no-op

            order.Status = OrderStatus.Cancel.GetStringValue();
            await _context.SaveChangesAsync();

            _ = WebSocketMiddleware.BroadcastOrderCancelled(order.Id);

            // If a driver was already assigned, free them up
            if (order.Driverid.HasValue)
            {
                _locationService.SetDriverAvailability(order.Driverid.Value, isAvailable: true);

                var driverToken = await GetDriverTokenByIdAsync(order.Driverid.Value);
                if (!string.IsNullOrEmpty(driverToken))
                {
                    await _notificationService.SendNotification(
                        driverToken,
                        "Order Cancelled",
                        "The user cancelled the order.",
                        BuildPayload("order_cancelled", order.Id, SnapshotOf(order)));
                }
            }
        }

        // ── Workflow transitions (Arrived → Started → Complete) ────────────────────

        public async Task HandleWorkflowTransitionAsync(UpdateOrderDriverDto dto, OrderStatus targetStatus)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            order.Status = targetStatus.GetStringValue();
            await _context.SaveChangesAsync();

            // Trip completed or administratively cancelled: driver is free again
            if (targetStatus is OrderStatus.Complete or OrderStatus.Cancel)
                _locationService.SetDriverAvailability(dto.Driverid, isAvailable: true);

            _ = WebSocketMiddleware.BroadcastOrderStatusUpdate(
                new { orderId = order.Id, status = order.Status, driverid = order.Driverid });

            var (notifType, userTitle, userBody, driverTitle, driverBody) = WorkflowMessages(targetStatus);
            var snap    = SnapshotOf(order);
            var payload = BuildPayload(notifType, order.Id, snap);

            var userToken = await GetUserTokenAsync(order.UserId) ?? order.FCMToken;
            if (!string.IsNullOrEmpty(userToken))
                await _notificationService.SendNotification(userToken, userTitle, userBody, payload);

            if (order.Driverid.HasValue)
            {
                var driverToken = await GetDriverTokenByIdAsync(order.Driverid.Value);
                if (!string.IsNullOrEmpty(driverToken))
                    await _notificationService.SendNotification(driverToken, driverTitle, driverBody, payload);
            }
        }

        // ── Queries ────────────────────────────────────────────────────────────────

        public async Task<List<OrderDto>> GetAllOrdersAsync() =>
            await _context.Orders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancel.GetStringValue())
                .Select(o => new OrderDto
                {
                    Id            = o.Id,
                    UserId        = o.UserId,
                    Date          = o.Date,
                    From          = o.From,
                    To            = o.To,
                    FromLatLng    = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
                    ToLatLng      = new LatLngDto { Lat = o.ToLatLng.Lat,   Lng = o.ToLatLng.Lng   },
                    ExpectedPrice = o.ExpectedPrice,
                    Type          = o.Type,
                    Distance      = o.Distance,
                    Notes         = o.Notes,
                    Review        = o.Review,
                    Driverid      = o.Driverid,
                    Status        = o.Status,
                    NoPassengers  = o.NoPassengers,
                    UserImage     = o.UserImage,
                    UserName      = o.UserName,
                    UserPhone     = o.UserPhone,
                    PaymentWay    = o.PaymentWay,
                    CarType       = o.CarType,
                    PinkMode      = o.PinkMode
                })
                .ToListAsync();

        public async Task<OrderDto> GetOrderByIdAsync(int id) =>
            await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(o => new OrderDto
                {
                    Id            = o.Id,
                    UserId        = o.UserId,
                    Date          = o.Date,
                    From          = o.From,
                    To            = o.To,
                    FromLatLng    = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
                    ToLatLng      = new LatLngDto { Lat = o.ToLatLng.Lat,   Lng = o.ToLatLng.Lng   },
                    ExpectedPrice = o.ExpectedPrice,
                    Type          = o.Type,
                    Distance      = o.Distance,
                    Notes         = o.Notes,
                    Review        = o.Review,
                    Driverid      = o.Driverid,
                    Status        = o.Status,
                    NoPassengers  = o.NoPassengers,
                    UserImage     = o.UserImage,
                    UserName      = o.UserName,
                    UserPhone     = o.UserPhone,
                    PaymentWay    = o.PaymentWay,
                    CarType       = o.CarType,
                    PinkMode      = o.PinkMode,
                    FCMToken      = o.FCMToken
                })
                .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Order not found");

        public async Task DeleteOrderAsync(int id)
        {
            var order = await _context.Orders.FindAsync(id)
                ?? throw new KeyNotFoundException("Order not found");

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        // ── Background notification (runs inside BackgroundJobProcessor) ──────────
        // Only accesses singleton services (_scopeFactory, _logger) and
        // closes over value types / the orderDto record — safe after request disposal.
        //
        // targetDriverIds == null  →  AllDrivers: WebSocket broadcasts to every
        //                             connected driver; FCM queries all driver tokens.
        // targetDriverIds != null  →  NearestOnly: targeted WebSocket + FCM only for
        //                             that specific set.  Empty list = no drivers
        //                             online nearby, notification is skipped.

        private async Task NotifyDriversAsync(
            OrderDto orderDto, List<int>? targetDriverIds, CancellationToken ct)
        {
            try
            {
                if (targetDriverIds is { Count: 0 })
                {
                    _logger.LogWarning(
                        "Order {OrderId}: NearestOnly mode but no available drivers nearby — skipping",
                        orderDto.Id);
                    return;
                }

                // WebSocket broadcast
                // empty list → middleware broadcasts to ALL order-socket connections
                // non-null list → targeted delivery only to those driver IDs
                var wsDriverIds = targetDriverIds ?? new List<int>();
                await WebSocketMiddleware.BroadcastNewOrderToDrivers(orderDto, wsDriverIds);

                // Fresh DI scope — the HTTP request scope is already disposed
                using var scope = _scopeFactory.CreateScope();
                var context             = scope.ServiceProvider.GetRequiredService<SnapDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                List<string> tokens;

                if (targetDriverIds is null)
                {
                    // AllDrivers: join every Driver to FCMTokenUsers
                    tokens = await context.Drivers
                        .AsNoTracking()
                        .Join(context.FCMTokenUsers,
                              d   => d.UserId,
                              fcm => fcm.UserId,
                              (_, fcm) => fcm.Token)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct()
                        .ToListAsync(ct);

                    _logger.LogInformation(
                        "Order {OrderId}: AllDrivers mode — notifying {Count} token(s)",
                        orderDto.Id, tokens.Count);
                }
                else
                {
                    // NearestOnly: join only the selected driver IDs
                    tokens = await context.Drivers
                        .AsNoTracking()
                        .Where(d => targetDriverIds.Contains(d.Id))
                        .Join(context.FCMTokenUsers,
                              d   => d.UserId,
                              fcm => fcm.UserId,
                              (_, fcm) => fcm.Token)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct()
                        .ToListAsync(ct);

                    _logger.LogInformation(
                        "Order {OrderId}: NearestOnly mode — notifying {Drivers} driver(s), {Tokens} token(s)",
                        orderDto.Id, targetDriverIds.Count, tokens.Count);
                }

                if (tokens.Count == 0) return;

                var payload = BuildPayload("new_order", orderDto.Id,
                    orderDto.UserName, orderDto.UserPhone,
                    orderDto.FromLatLng.Lat, orderDto.FromLatLng.Lng,
                    orderDto.ToLatLng.Lat,   orderDto.ToLatLng.Lng,
                    orderDto.ExpectedPrice,  orderDto.From, orderDto.To);

                await notificationService.SendBatchNotificationsAsync(
                    tokens,
                    "New Order Available",
                    "Check the app for a new trip request!",
                    payload,
                    NotificationBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Driver notification failed for order {OrderId}", orderDto.Id);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        // Returns the k nearest available online drivers.
        //
        // Two-stage approach for O(n) average, O(n log k) worst-case:
        //
        //   Stage 1 — Bounding box (pure arithmetic, no trig):
        //     ±maxLatDelta / ±maxLngDelta eliminates drivers outside a ~50 km square.
        //     In a typical city this cuts 80-95 % of candidates before any Haversine.
        //
        //   Stage 2 — PriorityQueue as a bounded max-heap (size = MaxNearbyDrivers):
        //     Walk the filtered candidates once; keep a heap of the k closest seen so
        //     far.  If a new driver is closer than the current k-th nearest, replace
        //     it.  This is O(n log k) vs O(n log n) for a full sort.
        //
        // For MaxNearbyDrivers = 10 and n = 2 000 drivers:
        //   Sort  → 2000 × log(2000) ≈ 22 000 comparisons
        //   Heap  → 2000 × log(10)   ≈  6 600 comparisons  (~3x faster)
        //   + bbox pre-filter makes the real n much smaller.
        private List<int> GetNearestDriverIds(double customerLat, double customerLng, int maxCount)
        {
            // Stage 1: cheap bounding-box — ~50 km radius expressed in degrees
            const double SearchRadiusKm = 50.0;
            var maxLatDelta = SearchRadiusKm / 111.0;
            var maxLngDelta = SearchRadiusKm / (111.0 * Math.Cos(ToRad(customerLat)));

            // Stage 2: bounded max-heap using PriorityQueue<driverId, -distance>.
            // PriorityQueue is a min-heap; negating distance turns it into a max-heap
            // so the root always holds the *farthest* driver in the current top-k set.
            var heap = new PriorityQueue<int, double>(maxCount + 1);

            foreach (var d in _locationService.GetOnlineDrivers())
            {
                // Bounding-box guard (fast path for distant drivers)
                if (Math.Abs(d.Lat - customerLat) > maxLatDelta ||
                    Math.Abs(d.Lng - customerLng) > maxLngDelta)
                    continue;

                var dist = HaversineKm(customerLat, customerLng, d.Lat, d.Lng);

                if (heap.Count < maxCount)
                {
                    heap.Enqueue(d.DriverId, -dist);   // negate → max-heap
                }
                else if (heap.TryPeek(out _, out var negMaxDist) && dist < -negMaxDist)
                {
                    heap.Dequeue();                     // drop current farthest
                    heap.Enqueue(d.DriverId, -dist);
                }
            }

            var result = new List<int>(heap.Count);
            while (heap.TryDequeue(out var id, out _))
                result.Add(id);
            return result;
        }

        private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            var dLat   = ToRad(lat2 - lat1);
            var dLng   = ToRad(lng2 - lng1);
            var sinLat = Math.Sin(dLat / 2);
            var sinLng = Math.Sin(dLng / 2);
            var a      = sinLat * sinLat
                       + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * sinLng * sinLng;
            return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180);

        private async Task<string?> GetUserTokenAsync(string userId) =>
            await _context.FCMTokenUsers
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .FirstOrDefaultAsync();

        private async Task<string?> GetDriverTokenByIdAsync(int driverId) =>
            await _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Join(_context.FCMTokenUsers,
                      d => d.UserId,
                      t => t.UserId,
                      (_, t) => t.Token)
                .FirstOrDefaultAsync();

        // Full-parameter overload used by NotifyNearbyDriversAsync (no OrderSnapshot available)
        private static Dictionary<string, string> BuildPayload(
            string type, int orderId,
            string? customerName, string? userPhone,
            double fromLat, double fromLng,
            double toLat,   double toLng,
            double price,   string from, string to) =>
            new()
            {
                { "type",           type                },
                { "orderId",        orderId.ToString()  },
                { "customerName",   customerName ?? ""  },
                { "userPhone",      userPhone    ?? ""  },
                { "customerLat",    fromLat.ToString()  },
                { "customerLng",    fromLng.ToString()  },
                { "destinationLat", toLat.ToString()    },
                { "destinationLng", toLng.ToString()    },
                { "price",          price.ToString()    },
                { "fromPlace",      from                },
                { "toPlace",        to                  }
            };

        // Snapshot-parameter overload used by status-change methods
        private static Dictionary<string, string> BuildPayload(string type, int orderId, OrderSnapshot s) =>
            BuildPayload(type, orderId, s.UserName, s.UserPhone,
                         s.FromLat, s.FromLng, s.ToLat, s.ToLng,
                         s.ExpectedPrice, s.From, s.To);

        private record OrderSnapshot
        {
            public string  UserId        { get; init; } = null!;
            public string? FCMToken      { get; init; }
            public string? UserName      { get; init; }
            public string? UserPhone     { get; init; }
            public double  ExpectedPrice { get; init; }
            public string  From          { get; init; } = null!;
            public string  To            { get; init; } = null!;
            public double  FromLat       { get; init; }
            public double  FromLng       { get; init; }
            public double  ToLat         { get; init; }
            public double  ToLng         { get; init; }
        }

        private static OrderSnapshot SnapshotOf(Order o) => new()
        {
            UserId        = o.UserId,
            FCMToken      = o.FCMToken,
            UserName      = o.UserName,
            UserPhone     = o.UserPhone,
            ExpectedPrice = o.ExpectedPrice,
            From          = o.From,
            To            = o.To,
            FromLat       = o.FromLatLng.Lat,
            FromLng       = o.FromLatLng.Lng,
            ToLat         = o.ToLatLng.Lat,
            ToLng         = o.ToLatLng.Lng
        };

        private static OrderDto MapToDto(Order o) => new()
        {
            Id            = o.Id,
            UserId        = o.UserId,
            Date          = o.Date,
            From          = o.From,
            To            = o.To,
            FromLatLng    = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
            ToLatLng      = new LatLngDto { Lat = o.ToLatLng.Lat,   Lng = o.ToLatLng.Lng   },
            ExpectedPrice = o.ExpectedPrice,
            Type          = o.Type,
            Distance      = o.Distance,
            Notes         = o.Notes,
            NoPassengers  = o.NoPassengers,
            UserImage     = o.UserImage,
            UserName      = o.UserName,
            UserPhone     = o.UserPhone,
            Status        = o.Status,
            Driverid      = o.Driverid,
            Review        = o.Review,
            PaymentWay    = o.PaymentWay,
            CarType       = o.CarType,
            PinkMode      = o.PinkMode,
            FCMToken      = o.FCMToken
        };

        private static (string type, string userTitle, string userBody, string driverTitle, string driverBody)
            WorkflowMessages(OrderStatus status) => status switch
        {
            OrderStatus.Arrived  => ("driver_arrived", "Driver Arrived",  "Your driver has arrived at the pickup location.", "Arrived",       "You have arrived at the pickup location."),
            OrderStatus.Started  => ("trip_started",   "Trip Started",    "Your trip has started. Enjoy the ride!",          "Trip Started",  "The trip has started."),
            OrderStatus.Complete => ("trip_completed",  "Trip Completed", "You have arrived at your destination.",            "Trip Completed","The trip has been completed."),
            _                    => ("update",          "Update",         "Order updated.",                                   "Update",        "Order updated.")
        };
    }
}
