using Snap.Application.Domain.Entities;

namespace Snap.Application.SavedAddresses.Interfaces
{
    public interface ISavedAddressRepository
    {
        void Add(SavedAddress address);
        Task<List<SavedAddress>> GetByUserOrderedByDateDescAsync(string userId);
        Task<List<SavedAddress>> GetTopByUsageAsync(string userId, int count);
        Task<List<SavedAddress>> GetFavoritesAsync(string userId);
        Task<SavedAddress?> GetByUserAndTitleTrackedAsync(string userId, string title);
        void Remove(SavedAddress address);
    }
}
