using Snap.Business.DTOs;
using Snap.Business.Interfaces;
using Snap.Core.Entities;
using Snap.DataAccess.Interfaces;

namespace Snap.Business.Services
{
    public class ExplorePlaceService : IExplorePlaceService
    {
        private readonly IRepository<ExplorePlace> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public ExplorePlaceService(IRepository<ExplorePlace> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<ExplorePlaceDto> CreateAsync(UpsertExplorePlaceDto dto)
        {
            var entity = new ExplorePlace
            {
                Name = dto.Name,
                Description = dto.Description,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Photo = dto.Photo,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
            return Map(entity);
        }

        public async Task<List<ExplorePlaceDto>> GetAllAsync() =>
            (await _repo.GetAllAsync()).Select(Map).ToList();

        public async Task<ExplorePlaceDto?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity is null ? null : Map(entity);
        }

        public async Task<ExplorePlaceDto?> UpdateAsync(int id, UpsertExplorePlaceDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity is null)
                return null;

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Latitude = dto.Latitude;
            entity.Longitude = dto.Longitude;
            entity.Photo = dto.Photo;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            _repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();
            return Map(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity is null)
                return false;

            _repo.Remove(entity);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        private static ExplorePlaceDto Map(ExplorePlace e) => new()
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            Photo = e.Photo,
            CreatedAtUtc = e.CreatedAtUtc,
            UpdatedAtUtc = e.UpdatedAtUtc
        };
    }
}
