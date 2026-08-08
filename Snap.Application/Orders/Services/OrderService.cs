using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snap.Application.Common.Interfaces.BackgroundJobs;
using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Domain.Entities;
using Snap.Application.Domain.Enums;
using Snap.Application.Domain.ValueObjects;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Orders.Mapping;
using Snap.Application.Orders.Settings;

namespace Snap.Application.Orders.Services
{
    public sealed class OrderService : IOrderService
    {
        private const double EarthRadiusKm = 6371.0;

        private readonly IOrderRepository            _orderRepo;
        private readonly IOrderWorkflowRepository    _workflowRepo;
        private readonly IUnitOfWork                 _unitOfWork;
        private readonly IDriverLocationService      _locationService;
        private readonly IOrderNotificationService   _notificationService;
        private readonly IServiceScopeFactory        _scopeFactory;
        private readonly IBackgroundJobQueue         _jobQueue;
        private readonly IOptions<OrderSettings>     _options;
        private readonly IRepository<CancelReason>   _cancelReasonRepo;

        public OrderService(
            IOrderRepository            orderRepo,
            IOrderWorkflowRepository    workflowRepo,
            IUnitOfWork                 unitOfWork,
            IDriverLocationService      locationService,
            IOrderNotificationService   notificationService,
            IServiceScopeFactory        scopeFactory,
            IBackgroundJobQueue         jobQueue,
            IOptions<OrderSettings>     options,
            IRepository<CancelReason>   cancelReasonRepo)
        {
            _orderRepo           = orderRepo;
            _workflowRepo        = workflowRepo;
            _unitOfWork          = unitOfWork;
            _locationService     = locationService;
            _notificationService = notificationService;
            _scopeFactory        = scopeFactory;
            _jobQueue            = jobQueue;
            _options             = options;
            _cancelReasonRepo    = cancelReasonRepo;
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

            var userInfo = await _orderRepo.GetUserSnapshotAsync(dto.UserId)
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

            _orderRepo.Add(order);
            await _unitOfWork.SaveChangesAsync();

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
            var rows = await _workflowRepo.TryAssignDriverAsync(
                dto.OrderId,
                dto.Driverid,
                OrderStatus.Approved.GetStringValue(),
                OrderStatus.Pending.GetStringValue());

            if (rows == 0)
                throw new InvalidOperationException("Order is no longer available or already accepted.");

            _locationService.SetDriverAvailability(dto.Driverid, isAvailable: false);

            var order = await _orderRepo.GetByIdProjectedAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found after accept.");

            await _notificationService.NotifyOrderAcceptedAsync(dto.OrderId, dto.Driverid, order);
        }

        public async Task AcceptScheduledOrderAsync(UpdateOrderDriverDto dto)
        {
            var order = await _orderRepo.GetStatusSnapshotAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            if (!string.Equals(order.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Order is not available for scheduled acceptance.");

            var settings = _options.Value;
            var conflictWindow = Math.Max(0, settings.ScheduledConflictWindowMinutes);

            var hasConflict = await _workflowRepo.HasSchedulingConflictAsync(dto.Driverid, order.Date, conflictWindow);

            if (hasConflict)
                throw new InvalidOperationException("Driver has another trip that conflicts with this scheduled time.");

            var rows = await _workflowRepo.TryAssignDriverAsync(
                dto.OrderId,
                dto.Driverid,
                "scheduled_accepted",
                "scheduled");

            if (rows == 0)
                throw new InvalidOperationException("Order is no longer available or already accepted.");

            var fullOrder = await _orderRepo.GetByIdProjectedAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found after accept.");

            await _notificationService.NotifyScheduledOrderAcceptedAsync(dto.OrderId, dto.Driverid, fullOrder);
        }

        // ── Driver cancels ────────────────────────────────────────────────────────

        public async Task CancelOrderByDriverAsync(UpdateOrderDriverDto dto)
        {
            var order = await _orderRepo.FindTrackedAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            order.Status = OrderStatus.Cancel.GetStringValue();
            await _unitOfWork.SaveChangesAsync();

            _locationService.SetDriverAvailability(dto.Driverid, isAvailable: true);
            await _notificationService.NotifyOrderCancelledAsync(dto.OrderId, OrderMapper.ToDto(order));
        }

        // ── User cancels ──────────────────────────────────────────────────────────

        public async Task CancelOrderByUserAsync(CancelOrderByUserDto dto)
        {
            var order = await _orderRepo.FindTrackedAsync(dto.OrderId)
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

            if (dto.CancelReasonId.HasValue &&
                await _cancelReasonRepo.GetByIdAsync(dto.CancelReasonId.Value) is null)
                throw new ArgumentException("Cancel reason not found.");

            order.Status = OrderStatus.Cancel.GetStringValue();
            order.CancelReasonId = dto.CancelReasonId;
            await _unitOfWork.SaveChangesAsync();

            if (order.Driverid.HasValue)
                _locationService.SetDriverAvailability(order.Driverid.Value, isAvailable: true);

            await _notificationService.NotifyOrderCancelledAsync(dto.OrderId, OrderMapper.ToDto(order));
        }

        // ── Workflow transitions (Arrived → Started → Complete) ───────────────────

        public async Task HandleWorkflowTransitionAsync(UpdateOrderDriverDto dto, OrderStatus targetStatus)
        {
            var order = await _orderRepo.FindTrackedAsync(dto.OrderId)
                ?? throw new KeyNotFoundException("Order not found");

            order.Status = targetStatus.GetStringValue();
            await _unitOfWork.SaveChangesAsync();

            if (targetStatus is OrderStatus.Complete or OrderStatus.Cancel)
                _locationService.SetDriverAvailability(dto.Driverid, isAvailable: true);

            await _notificationService.NotifyWorkflowTransitionAsync(dto.OrderId, OrderMapper.ToDto(order), targetStatus);
        }

        // ── Queries ───────────────────────────────────────────────────────────────

        public Task<List<OrderDto>> GetAllOrdersAsync() => _orderRepo.GetAllActiveProjectedAsync();

        public Task<List<OrderDto>> GetScheduledOrdersByUserAsync(string userId) =>
            _orderRepo.GetScheduledForUserProjectedAsync(userId);

        public async Task<OrderDto> GetOrderByIdAsync(int id) =>
            await _orderRepo.GetByIdProjectedAsync(id)
            ?? throw new KeyNotFoundException("Order not found");

        public async Task DeleteOrderAsync(int id)
        {
            var order = await _orderRepo.FindTrackedAsync(id)
                ?? throw new KeyNotFoundException("Order not found");

            _orderRepo.Remove(order);
            await _unitOfWork.SaveChangesAsync();
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
