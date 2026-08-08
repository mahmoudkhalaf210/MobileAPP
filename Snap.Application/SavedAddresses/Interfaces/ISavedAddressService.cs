using Snap.Application.Domain.Entities;
using Snap.Application.SavedAddresses.DTOs;

namespace Snap.Application.SavedAddresses.Interfaces
{
    public interface ISavedAddressService
    {
        Task<SavedAddress> AddAddressAsync(SavedAddress address);
        Task<List<SavedAddress>> GetUserAddressesAsync(string userId);
        Task<List<SavedAddress>> GetSuggestionsAsync(string userId);
        Task<(SavedAddress? Home, SavedAddress? Work)> GetFavoritesAsync(string userId);
        Task<SavedAddress> UpsertFavoriteAsync(string userId, string title, UpsertFavoriteLocationDto dto);
        Task DeleteFavoriteAsync(string userId, string title);
    }
}
