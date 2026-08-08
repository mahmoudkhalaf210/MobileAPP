using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Entities;
using Snap.Application.SavedAddresses.Interfaces;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class SavedAddressRepository : ISavedAddressRepository
    {
        private readonly SnapDbContext _context;

        public SavedAddressRepository(SnapDbContext context)
        {
            _context = context;
        }

        public void Add(SavedAddress address) => _context.SavedAddresses.Add(address);

        public Task<List<SavedAddress>> GetByUserOrderedByDateDescAsync(string userId) =>
            _context.SavedAddresses
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

        public Task<List<SavedAddress>> GetTopByUsageAsync(string userId, int count) =>
            _context.SavedAddresses
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UsageCount)
                .Take(count)
                .ToListAsync();

        public Task<List<SavedAddress>> GetFavoritesAsync(string userId) =>
            _context.SavedAddresses
                .AsNoTracking()
                .Where(x => x.UserId == userId && (x.Title == "Home" || x.Title == "Work"))
                .ToListAsync();

        public Task<SavedAddress?> GetByUserAndTitleTrackedAsync(string userId, string title) =>
            _context.SavedAddresses
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Title == title);

        public void Remove(SavedAddress address) => _context.SavedAddresses.Remove(address);
    }
}
