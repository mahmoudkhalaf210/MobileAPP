using Microsoft.EntityFrameworkCore;
using Snap.Application.Orders.Interfaces;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class OrderNotificationRepository : IOrderNotificationRepository
    {
        private readonly SnapDbContext _context;

        public OrderNotificationRepository(SnapDbContext context)
        {
            _context = context;
        }

        public Task<string?> GetDriverNameAsync(int driverId) =>
            _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Select(d => d.DriverFullname)
                .FirstOrDefaultAsync();

        public Task<string?> GetUserFcmTokenAsync(string userId) =>
            _context.FCMTokenUsers
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .FirstOrDefaultAsync();

        public Task<string?> GetDriverFcmTokenAsync(int driverId) =>
            _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Join(_context.FCMTokenUsers,
                      d => d.UserId,
                      t => t.UserId,
                      (_, t) => t.Token)
                .FirstOrDefaultAsync();

        public Task<List<string>> GetAllDriverTokensAsync(CancellationToken ct) =>
            _context.Drivers
                .AsNoTracking()
                .Join(_context.FCMTokenUsers,
                      d   => d.UserId,
                      fcm => fcm.UserId,
                      (_,  fcm) => fcm.Token)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToListAsync(ct);

        public Task<List<string>> GetTargetDriverTokensAsync(IReadOnlyList<int> driverIds, CancellationToken ct) =>
            _context.Drivers
                .AsNoTracking()
                .Where(d => driverIds.Contains(d.Id))
                .Join(_context.FCMTokenUsers,
                      d   => d.UserId,
                      fcm => fcm.UserId,
                      (_,  fcm) => fcm.Token)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToListAsync(ct);
    }
}
