using Snap.Application.Domain.Entities;
using Snap.Application.UserHistory.DTOs;

namespace Snap.Application.UserHistory.Interfaces
{
    public interface IUserHistoryRepository
    {
        Task<User?> GetUserByIdAsync(string userId);
        void Add(Domain.Entities.UserHistory history);
        Task<List<Domain.Entities.UserHistory>> GetAllAsync();
        Task<Domain.Entities.UserHistory?> GetByIdAsync(int id);
        Task<List<UserHistoryDetailsDto>> GetUserHistoryDetailsAsync(string userId);
    }
}
