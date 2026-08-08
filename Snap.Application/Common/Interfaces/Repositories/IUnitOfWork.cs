namespace Snap.Application.Common.Interfaces.Repositories
{
    // Wraps the shared SnapDbContext's commit boundary for the generic-repository
    // (Add/Update/Remove-tracked-but-not-yet-saved) code paths, e.g. Explore CRUD.
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
