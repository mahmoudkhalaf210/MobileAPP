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

            var settings  = _options.Value;
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
