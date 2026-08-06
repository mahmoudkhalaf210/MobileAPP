using Snap.DataAccess.Interfaces;
using Snap.Repository.Data;

namespace Snap.DataAccess.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SnapDbContext _context;

        public UnitOfWork(SnapDbContext context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
    }
}
