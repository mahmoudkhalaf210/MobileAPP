using Snap.Application.Common.Interfaces.Repositories;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
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
