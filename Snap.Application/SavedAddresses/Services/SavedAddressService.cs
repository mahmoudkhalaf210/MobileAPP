using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Domain.Entities;
using Snap.Application.SavedAddresses.DTOs;
using Snap.Application.SavedAddresses.Interfaces;

namespace Snap.Application.SavedAddresses.Services
{
    public class SavedAddressService : ISavedAddressService
    {
        private readonly ISavedAddressRepository _repo;
        private readonly IUnitOfWork _unitOfWork;

        public SavedAddressService(ISavedAddressRepository repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<SavedAddress> AddAddressAsync(SavedAddress address)
        {
            _repo.Add(address);
            await _unitOfWork.SaveChangesAsync();
            return address;
        }

        public Task<List<SavedAddress>> GetUserAddressesAsync(string userId) =>
            _repo.GetByUserOrderedByDateDescAsync(userId);

        public Task<List<SavedAddress>> GetSuggestionsAsync(string userId) =>
            _repo.GetTopByUsageAsync(userId, 5);

        public async Task<(SavedAddress? Home, SavedAddress? Work)> GetFavoritesAsync(string userId)
        {
            var favorites = await _repo.GetFavoritesAsync(userId);
            var home = favorites.FirstOrDefault(x => x.Title == "Home");
            var work = favorites.FirstOrDefault(x => x.Title == "Work");
            return (home, work);
        }

        public async Task<SavedAddress> UpsertFavoriteAsync(string userId, string title, UpsertFavoriteLocationDto dto)
        {
            var normalizedTitle = NormalizeFavoriteTitle(title)
                ?? throw new ArgumentException("Title must be Home or Work.");

            var existing = await _repo.GetByUserAndTitleTrackedAsync(userId, normalizedTitle);

            if (existing == null)
            {
                var entity = new SavedAddress
                {
                    UserId = userId,
                    Title = normalizedTitle,
                    AddressLine = dto.AddressLine,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    CreatedAt = DateTime.UtcNow,
                    UsageCount = 0
                };

                _repo.Add(entity);
                await _unitOfWork.SaveChangesAsync();
                return entity;
            }

            existing.AddressLine = dto.AddressLine;
            existing.Latitude = dto.Latitude;
            existing.Longitude = dto.Longitude;
            await _unitOfWork.SaveChangesAsync();

            return existing;
        }

        public async Task DeleteFavoriteAsync(string userId, string title)
        {
            var normalizedTitle = NormalizeFavoriteTitle(title)
                ?? throw new ArgumentException("Title must be Home or Work.");

            var existing = await _repo.GetByUserAndTitleTrackedAsync(userId, normalizedTitle)
                ?? throw new KeyNotFoundException("Favorite location not found.");

            _repo.Remove(existing);
            await _unitOfWork.SaveChangesAsync();
        }

        private static string? NormalizeFavoriteTitle(string title)
        {
            if (string.Equals(title, "home", StringComparison.OrdinalIgnoreCase))
                return "Home";
            if (string.Equals(title, "work", StringComparison.OrdinalIgnoreCase))
                return "Work";
            return null;
        }
    }
}
