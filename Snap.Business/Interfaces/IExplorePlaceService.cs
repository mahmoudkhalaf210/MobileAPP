using Snap.Business.DTOs;

namespace Snap.Business.Interfaces
{
    public interface IExplorePlaceService
    {
        Task<ExplorePlaceDto> CreateAsync(UpsertExplorePlaceDto dto);
        Task<List<ExplorePlaceDto>> GetAllAsync();
        Task<ExplorePlaceDto?> GetByIdAsync(int id);
        Task<ExplorePlaceDto?> UpdateAsync(int id, UpsertExplorePlaceDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
