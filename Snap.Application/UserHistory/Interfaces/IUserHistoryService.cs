using Snap.Application.UserHistory.DTOs;

namespace Snap.Application.UserHistory.Interfaces
{
    public interface IUserHistoryService
    {
        Task<UserHistoryDto> CreateUserHistoryAsync(CreateUserHistoryDto dto);
        Task<List<UserHistoryDto>> GetAllUserHistoriesAsync();
        Task<List<UserHistoryDetailsDto>> GetUserHistoriesByUserIdAsync(string userId);
        Task<UserHistoryDto> GetUserHistoryByIdAsync(int id);
    }
}
