using Microsoft.EntityFrameworkCore;
using Snap.Application.Points.Interfaces;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class DriverPointsRepository : IDriverPointsRepository
    {
        private readonly SnapDbContext _context;

        public DriverPointsRepository(SnapDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetBalanceAsync(int driverId) =>
            await _context.DriverPoints
                .AsNoTracking()
                .Where(p => p.DriverId == driverId)
                .Select(p => p.Balance)
                .FirstOrDefaultAsync();

        public async Task<bool> TryAwardPointsForOrderAsync(int orderId, int driverId, int points)
        {
            // Guarded insert: the unique index on OrderId (ConfigureDriverPointsTransaction) is the
            // hard idempotency guarantee — this WHERE NOT EXISTS is the fast-path check that avoids
            // hitting it in the common (non-racing) case of a single completion event per order.
            var insertedRows = await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO DriverPointsTransactions (DriverId, OrderId, PointsAwarded, CreatedAtUtc) " +
                "SELECT {0}, {1}, {2}, SYSUTCDATETIME() " +
                "WHERE NOT EXISTS (SELECT 1 FROM DriverPointsTransactions WHERE OrderId = {1})",
                driverId, orderId, points);

            if (insertedRows == 0)
                return false; // already awarded for this order — idempotent no-op

            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE DriverPoints SET Balance = Balance + {1}, UpdatedAtUtc = SYSUTCDATETIME() WHERE DriverId = {0}; " +
                "IF @@ROWCOUNT = 0 INSERT INTO DriverPoints (DriverId, Balance, UpdatedAtUtc) VALUES ({0}, {1}, SYSUTCDATETIME());",
                driverId, points);

            return true;
        }
    }
}
