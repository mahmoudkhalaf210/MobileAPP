using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Snap.DataAccess.Interfaces;
using Snap.Repository.Data;

namespace Snap.DataAccess.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbSet<T> _set;

        public Repository(SnapDbContext context)
        {
            _set = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(object id) => await _set.FindAsync(id);

        public async Task<List<T>> GetAllAsync() => await _set.AsNoTracking().ToListAsync();

        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
            await _set.AsNoTracking().Where(predicate).ToListAsync();

        public async Task AddAsync(T entity) => await _set.AddAsync(entity);

        public void Update(T entity) => _set.Update(entity);

        public void Remove(T entity) => _set.Remove(entity);
    }
}
