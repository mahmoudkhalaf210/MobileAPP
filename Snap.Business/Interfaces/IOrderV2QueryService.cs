using Snap.Business.DTOs;

namespace Snap.Business.Interfaces
{
    public interface IOrderV2QueryService
    {
        Task<List<OrderV2Dto>> GetAllOrdersAsync();
        Task<OrderV2Dto?> GetByIdAsync(int id);
        Task<List<OrderV2Dto>> GetScheduledForUserAsync(string userId);
        Task<List<OrderV2Dto>> GetActiveForUserAsync(string userId);
        Task<List<OrderV2Dto>> GetCompletedForUserAsync(string userId);
        Task<List<OrderV2Dto>> GetCancelledForUserAsync(string userId);
    }
}
