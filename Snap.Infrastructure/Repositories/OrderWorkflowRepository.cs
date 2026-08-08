using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.Interfaces;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class OrderWorkflowRepository : IOrderWorkflowRepository
    {
        private readonly SnapDbContext _context;

        public OrderWorkflowRepository(SnapDbContext context)
        {
            _context = context;
        }

        public Task<int> TryAssignDriverAsync(int orderId, int driverId, string newStatus, string requiredCurrentStatus) =>
            _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET Driverid = {0}, Status = {1} WHERE Id = {2} AND Status = {3}",
                driverId, newStatus, orderId, requiredCurrentStatus);

        public Task<int> TryTransitionStatusAsync(int orderId, string newStatus, string requiredCurrentStatus) =>
            _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET Status = {0} WHERE Id = {1} AND Status = {2}",
                newStatus, orderId, requiredCurrentStatus);

        public Task<bool> HasSchedulingConflictAsync(int driverId, DateTime referenceDate, int conflictWindowMinutes) =>
            _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.Driverid == driverId &&
                    (o.Status == "scheduled_accepted" || o.Status == OrderStatus.Approved.GetStringValue() || o.Status == OrderStatus.Arrived.GetStringValue() || o.Status == OrderStatus.Started.GetStringValue()) &&
                    EF.Functions.DateDiffMinute(o.Date, referenceDate) <= conflictWindowMinutes &&
                    EF.Functions.DateDiffMinute(o.Date, referenceDate) >= -conflictWindowMinutes)
                .AnyAsync();
    }
}
