using Microsoft.EntityFrameworkCore;
using Snap.Core.Entities;
using Snap.DataAccess.Interfaces;
using Snap.Repository.Data;

namespace Snap.DataAccess.Repositories
{
    public class OrderRepositoryV2 : IOrderRepositoryV2
    {
        private readonly SnapDbContext _context;

        public OrderRepositoryV2(SnapDbContext context)
        {
            _context = context;
        }

        public Task SetCarTypeEnumAsync(int orderId, CarType carType) =>
            _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET CarTypeEnum = {0} WHERE Id = {1}",
                carType.ToString(), orderId);

        public Task<Order?> GetByIdAsync(int id) =>
            _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);

        // Mirrors the legacy OrderService.GetAllOrdersAsync semantics (all non-cancelled orders).
        public Task<List<Order>> GetAllActiveOrdersAsync() =>
            _context.Orders.AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancel.GetStringValue())
                .OrderByDescending(o => o.Date)
                .ToListAsync();

        // Mirrors the legacy OrderService.GetScheduledOrdersByUserAsync semantics.
        public Task<List<Order>> GetScheduledForUserAsync(string userId)
        {
            var nowUtc = DateTime.UtcNow;
            return _context.Orders.AsNoTracking()
                .Where(o =>
                    o.UserId == userId &&
                    (o.Status == "scheduled" ||
                     o.Status == "scheduled_accepted" ||
                     (o.Status == OrderStatus.Pending.GetStringValue() && o.Date >= nowUtc)))
                .OrderBy(o => o.Date)
                .ToListAsync();
        }

        // Active = anything not yet completed or cancelled (pending/approve/Arrived/Started/scheduled/scheduled_accepted).
        public Task<List<Order>> GetActiveForUserAsync(string userId) =>
            _context.Orders.AsNoTracking()
                .Where(o =>
                    o.UserId == userId &&
                    o.Status != OrderStatus.Complete.GetStringValue() &&
                    o.Status != OrderStatus.Cancel.GetStringValue())
                .OrderByDescending(o => o.Date)
                .ToListAsync();

        public Task<List<Order>> GetCompletedForUserAsync(string userId) =>
            _context.Orders.AsNoTracking()
                .Where(o => o.UserId == userId && o.Status == OrderStatus.Complete.GetStringValue())
                .OrderByDescending(o => o.Date)
                .ToListAsync();

        public Task<List<Order>> GetCancelledForUserAsync(string userId) =>
            _context.Orders.AsNoTracking()
                .Where(o => o.UserId == userId && o.Status == OrderStatus.Cancel.GetStringValue())
                .OrderByDescending(o => o.Date)
                .ToListAsync();
    }
}
