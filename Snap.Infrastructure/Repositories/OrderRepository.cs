using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Entities;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Orders.Mapping;
using Snap.Application.Orders.Models;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly SnapDbContext _context;

        public OrderRepository(SnapDbContext context)
        {
            _context = context;
        }

        public async Task<OrderUserSnapshot?> GetUserSnapshotAsync(string userId)
        {
            var u = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Image, u.FullName, u.PhoneNumber })
                .FirstOrDefaultAsync();

            return u is null ? null : new OrderUserSnapshot { Image = u.Image, FullName = u.FullName, PhoneNumber = u.PhoneNumber };
        }

        public void Add(Order order) => _context.Orders.Add(order);

        public async Task<Order?> FindTrackedAsync(int id) => await _context.Orders.FindAsync(id);

        public void Remove(Order order) => _context.Orders.Remove(order);

        public Task<OrderDto?> GetByIdProjectedAsync(int id, CancellationToken ct = default) =>
            _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(OrderMapper.ToProjection)
                .FirstOrDefaultAsync(ct);

        public Task<List<OrderDto>> GetAllActiveProjectedAsync() =>
            _context.Orders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancel.GetStringValue())
                .Select(OrderMapper.ToProjection)
                .ToListAsync();

        public Task<List<OrderDto>> GetScheduledForUserProjectedAsync(string userId)
        {
            var nowUtc = DateTime.UtcNow;
            return _context.Orders
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

        public async Task<OrderStatusSnapshot?> GetStatusSnapshotAsync(int id)
        {
            var o = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(o => new { o.Id, o.Status, o.Date })
                .FirstOrDefaultAsync();

            return o is null ? null : new OrderStatusSnapshot { Id = o.Id, Status = o.Status, Date = o.Date };
        }

        public async Task<List<ScheduledOrderReminderInfo>> GetDueForReminderAsync(DateTime nowUtc, DateTime reminderThresholdUtc, CancellationToken ct)
        {
            var rows = await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.Status == "scheduled_accepted" &&
                    o.Driverid != null &&
                    o.Date >= nowUtc &&
                    o.Date <= reminderThresholdUtc)
                .Select(o => new { o.Id, DriverId = o.Driverid!.Value, o.Date })
                .ToListAsync(ct);

            return rows.Select(o => new ScheduledOrderReminderInfo { Id = o.Id, DriverId = o.DriverId, Date = o.Date }).ToList();
        }

        public async Task<List<ScheduledOrderStartingSoonInfo>> GetDueForStartingSoonAsync(DateTime startingSoonThresholdUtc, CancellationToken ct)
        {
            var rows = await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.Status == "scheduled_accepted" &&
                    o.Driverid != null &&
                    o.Date <= startingSoonThresholdUtc)
                .Select(o => new { o.Id, DriverId = o.Driverid!.Value })
                .ToListAsync(ct);

            return rows.Select(o => new ScheduledOrderStartingSoonInfo { Id = o.Id, DriverId = o.DriverId }).ToList();
        }

        public async Task<List<int>> GetBusyDriverIdsAsync(CancellationToken ct)
        {
            var ActiveStatuses = new[]
            {
                OrderStatus.Approved.GetStringValue(),
                OrderStatus.Arrived.GetStringValue(),
                OrderStatus.Started.GetStringValue()
            };

            // EF Core has no ToHashSetAsync — materialise to list then convert (caller dedupes if needed)
            return await _context.Orders
                .AsNoTracking()
                .Where(o => ActiveStatuses.Contains(o.Status) && o.Driverid.HasValue)
                .Select(o => o.Driverid!.Value)
                .Distinct()
                .ToListAsync(ct);
        }

        public Task<List<Order>> GetExpiredPendingTrackedAsync(DateTime cutoffUtc, CancellationToken ct)
        {
            var pendingStatus = OrderStatus.Pending.GetStringValue();
            return _context.Orders
                .Where(o => o.Status == pendingStatus && o.Date < cutoffUtc)
                .ToListAsync(ct);
        }

        public Task<int> DeleteExpiredPendingOrdersRawAsync(CancellationToken ct)
        {
            // Uses SQL Server's GETDATE() to ensure time comparison is done using database server time.
            var sqlQuery = @"
                    delete from Orders
                    WHERE Status = 'pending'
                      AND Date AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time'
                          <= FORMAT(DATEADD(MINUTE, -4,
                              (GETDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time')
                          ), 'yyyy-MM-dd HH:mm')
                   ";

            return _context.Database.ExecuteSqlRawAsync(sqlQuery, ct);
        }
    }
}
