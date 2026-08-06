using Microsoft.EntityFrameworkCore;
using Snap.DataAccess.Interfaces;
using Snap.Repository.Data;

namespace Snap.DataAccess.Repositories
{
    public class UserPointsRepository : IUserPointsRepository
    {
        private readonly SnapDbContext _context;

        public UserPointsRepository(SnapDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetBalanceAsync(string userId) =>
            await _context.UserPoints
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => p.Balance)
                .FirstOrDefaultAsync();

        public async Task<bool> TryAwardPointsForOrderAsync(int orderId, string userId, int points)
        {
            // Guarded insert: the unique index on OrderId (ConfigureUserPointsTransaction) is the
            // hard idempotency guarantee — this WHERE NOT EXISTS is the fast-path check that avoids
            // hitting it in the common (non-racing) case of a single completion event per order.
            var insertedRows = await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO UserPointsTransactions (UserId, OrderId, PointsAwarded, CreatedAtUtc) " +
                "SELECT {0}, {1}, {2}, SYSUTCDATETIME() " +
                "WHERE NOT EXISTS (SELECT 1 FROM UserPointsTransactions WHERE OrderId = {1})",
                userId, orderId, points);

            if (insertedRows == 0)
                return false; // already awarded for this order — idempotent no-op

            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE UserPoints SET Balance = Balance + {1}, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = {0}; " +
                "IF @@ROWCOUNT = 0 INSERT INTO UserPoints (UserId, Balance, UpdatedAtUtc) VALUES ({0}, {1}, SYSUTCDATETIME());",
                userId, points);

            return true;
        }
    }
}
