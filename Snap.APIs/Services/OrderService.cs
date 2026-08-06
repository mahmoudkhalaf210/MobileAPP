using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Snap.APIs.DTOs;
using Snap.APIs.Mapping;
using Snap.APIs.Settings;
using Snap.Core.Entities;
using Snap.Repository.Data;

namespace Snap.APIs.Services
{
    public sealed class OrderService : IOrderService
    {
        private const double EarthRadiusKm = 6371.0;

        private readonly SnapDbContext             _context;
        private readonly IDriverLocationService    _locationService;
        private readonly IOrderNotificationService _notificationService;
        private readonly IServiceScopeFactory      _scopeFactory;
        private readonly IBackgroundJobQueue       _jobQueue;
        private readonly IOptions<OrderSettings>   _options;

        public OrderService(
            SnapDbContext              context,
            IDriverLocationService     locationService,
            IOrderNotificationService  notificationService,
            IServiceScopeFactory       scopeFactory,
            IBackgroundJobQueue        jobQueue,
            IOptions<OrderSettings>    options)
        {
            _context             = context;
            _locationService     = locationService;
            _notificationService = notificationService;
            _scopeFactory        = scopeFactory;
            _jobQueue            = jobQueue;
            _options             = options;
        }

        // ── Create ────────────────────────────────────────────────────────────────

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Type))
                throw new ArgumentException("Order type is required.");

            var nowUtc = DateTime.UtcNow;
            var settings = _options.Value;
            var leadTime = TimeSpan.FromMinutes(Math.Max(0, settings.ScheduledDispatchLeadTimeMinutes));
            var isScheduled = dto.Date > nowUtc.Add(leadTime);

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
                Status        = isScheduled ? "scheduled" : OrderStatus.Pending.GetStringValue(),
                PaymentWay    = dto.PaymentWay,
                CarType       = dto.CarType,
                PinkMode      = dto.PinkMode,
                FCMToken      = dto.FCMToken
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var orderDto  = OrderMapper.ToDto(order);

            var targetIds = settings.NotificationMode == DriverNotificationMode.NearestOnly
                ? GetNearestDriverIds(dto.FromLatLng.Lat, dto.FromLatLng.Lng, settings.NearestDriverCount)
                : null;

            // Enqueue runs after the HTTP response returns. The lambda captures only
            // value types + _scopeFactory (singleton) — safe after request scope disposal.
            await _jobQueue.EnqueueAsync(async ct =>
            {
                using var scope   = _scopeFactory.CreateScope();
                var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotificationService>();
                if (isScheduled)
                    await notifier.NotifyDriversOfScheduledOrderAsync(orderDto, targetIds, ct);
                else
                    await notifier.NotifyDriversOfNewOrderAsync(orderDto, targetIds, ct);
            });

            return orderDto;
        }

        // ── Accept (atomic, race-condition-safe) ──────────────────────────────────

        public async Task AcceptOrderAsync(UpdateOrderDriverDto dto)
        {
            var rows = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET Driverid = {0}, Status = {1} WHERE Id = {2} AND Status = {3}",
                dto.Driverid,
                OrderStatus.Approved.GetStringValue(),
                dto.OrderId,
                OrderStatus.Pending.GetStringValue());

            if (rows == 0)
                throw new InvalidOperationException("Order is no longer available or already accepted.");

            _locationService.SetDriverAvailability(dto.Driverid, isAvailable: false);

            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == dto.OrderId)
                .Select(OrderMapper.ToProjection)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Order not found after accept.");

            await _notificationService.NotifyOrderAcceptedAsync(dto.OrderId, dto.Driverid, order);
        }

        public async Task AcceptScheduledOrderAsync(UpdateOrderDriverDto dto)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == dto.OrderId)
                .Select(o => new { o.Id, o.Status, o.Date })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Order not found");

            if (!string.Equals(order.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Order is not available for scheduled acceptance.");

            var settings = _options.Value;
            var conflictWindow = Math.Max(0, settings.ScheduledConflictWindowMinutes);

            var hasConflict = await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.Driverid == dto.Driverid &&
                    (o.Status == "scheduled_accepted" || o.Status == OrderStatus.Approved.GetStringValue() || o.Status == OrderStatus.Arrived.GetStringValue() || o.Status == OrderStatus.Started.GetStringValue()) &&
                    EF.Functions.DateDiffMinute(o.Date, order.Date) <= conflictWindow &&
                    EF.Functions.DateDiffMinute(o.Date, order.Date) >= -conflictWindow)
                .AnyAsync();

            if (hasConflict)
                throw new InvalidOperationException("Driver has another trip that conflicts with this scheduled time.");

            var rows = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET Driverid = {0}, Status = {1} WHERE Id = {2} AND Status = {3}",
                dto.Driverid,
                "scheduled_accepted",
                dto.OrderId,
                "scheduled");

            if (rows == 0)
                throw new InvalidOperationException("Order is no longer available or already accepted.");

            var fullOrder = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == dto.OrderId)
                .Select(OrderMapper.ToProjection)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Order not found after accept.");

            await _notificationService.NotifyScheduledOrderAcceptedAsync(dto.OrderId, dto.Driverid, fullOrder);
        }

        // ── Driver cancels ────────────────────────────────────────────────────────

        public async Task CancelOrderByDriverAsync(UpdateOrderDriverDto dto)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            order.Status = OrderStatus.Cancel.GetStringValue();
            await _context.SaveChangesAsync();

            _locationService.SetDriverAvailability(dto.Driverid, isAvailable: true);
            await _notificationService.NotifyOrderCancelledAsync(dto.OrderId, OrderMapper.ToDto(order));
        }

        // ── User cancels ──────────────────────────────────────────────────────────

        public async Task CancelOrderByUserAsync(CancelOrderByUserDto dto)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            if (order.UserId != dto.UserId)
                throw new UnauthorizedAccessException("You are not allowed to cancel this order.");

            if (string.Equals(order.Status, "scheduled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(order.Status, "scheduled_accepted", StringComparison.OrdinalIgnoreCase))
            {
                var settings = _options.Value;
                var cutoffMinutes = Math.Max(0, settings.ScheduledCancelCutoffMinutes);
                var cutoffTimeUtc = order.Date.AddMinutes(-cutoffMinutes);

                if (DateTime.UtcNow > cutoffTimeUtc)
                    throw new InvalidOperationException($"You can only cancel a scheduled trip before {cutoffMinutes} minutes of its start time.");
            }

            var current = OrderStatusExtensions.FromString(order.Status ?? string.Empty);

            if (current == OrderStatus.Complete)
                throw new InvalidOperationException("You cannot cancel a completed order.");

            if (current == OrderStatus.Cancel)
                return;

            order.Status = OrderStatus.Cancel.GetStringValue();
            await _context.SaveChangesAsync();

            if (order.Driverid.HasValue)
                _locationService.SetDriverAvailability(order.Driverid.Value, isAvailable: true);

            await _notificationService.NotifyOrderCancelledAsync(dto.OrderId, OrderMapper.ToDto(order));
        }

        // ── Workflow transitions (Arrived → Started → Complete) ───────────────────

        public async Task HandleWorkflowTransitionAsync(UpdateOrderDriverDto dto, OrderStatus targetStatus)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            order.Status = targetStatus.GetStringValue();
            await _context.SaveChangesAsync();

            if (targetStatus is OrderStatus.Complete or OrderStatus.Cancel)
                _locationService.SetDriverAvailability(dto.Driverid, isAvailable: true);

            await _notificationService.NotifyWorkflowTransitionAsync(dto.OrderId, OrderMapper.ToDto(order), targetStatus);
        }

        // ── Queries ───────────────────────────────────────────────────────────────

        public async Task<List<OrderDto>> GetAllOrdersAsync() =>
            await _context.Orders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancel.GetStringValue())
                .Select(OrderMapper.ToProjection)
                .ToListAsync();

        public async Task<List<OrderDto>> GetScheduledOrdersByUserAsync(string userId)
        {
            var nowUtc = DateTime.UtcNow;
            return await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.UserId == userId &&
                    (o.Status == "scheduled" ||
                     o.Status == "scheduled_accepted" ||
                     (o.Status == "pending" && o.Date >= nowUtc)))
                .OrderBy(o => o.Date)
                .Select(OrderMapper.ToProjection)
                .ToListAsync();
        }

        public async Task<OrderDto> GetOrderByIdAsync(int id) =>
            await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(OrderMapper.ToProjection)
                .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Order not found");

        public async Task DeleteOrderAsync(int id)
        {
            var order = await _context.Orders.FindAsync(id)
                ?? throw new KeyNotFoundException("Order not found");

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        // ── Nearest-driver selection ──────────────────────────────────────────────
        //
        // Two-stage O(n log k) algorithm:
        //   Stage 1: bounding-box (pure arithmetic) eliminates ~90% of candidates.
        //   Stage 2: PriorityQueue as a bounded max-heap keeps top-k without a
        //            full sort — O(n log k) vs O(n log n).

        private IReadOnlyList<int> GetNearestDriverIds(double customerLat, double customerLng, int maxCount)
        {
            const double SearchRadiusKm = 50.0;
            var maxLatDelta = SearchRadiusKm / 111.0;
            var maxLngDelta = SearchRadiusKm / (111.0 * Math.Cos(ToRad(customerLat)));

            // Negate distance → min-heap becomes a max-heap so the root is always
            // the farthest driver in the current top-k set.
            var heap = new PriorityQueue<int, double>(maxCount + 1);

            foreach (var d in _locationService.GetOnlineDrivers())
            {
                if (Math.Abs(d.Lat - customerLat) > maxLatDelta ||
                    Math.Abs(d.Lng - customerLng) > maxLngDelta)
                    continue;

                var dist = HaversineKm(customerLat, customerLng, d.Lat, d.Lng);

                if (heap.Count < maxCount)
                    heap.Enqueue(d.DriverId, -dist);
                else if (heap.TryPeek(out _, out var negMax) && dist < -negMax)
                {
                    heap.Dequeue();
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
            var a      = sinLat * sinLat + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * sinLng * sinLng;
            return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180);
    }
}
