using Snap.Core.Entities;

namespace Snap.DataAccess.Interfaces
{
    // Read/insert-only for the v2 surface. Deliberately excludes Status mutation —
    // all order status transitions remain exclusively inside the existing, untouched
    // Snap.APIs.Services.OrderService, which owns the race-safe atomic-update logic.
    public interface IOrderRepositoryV2
    {
        Task SetCarTypeEnumAsync(int orderId, CarType carType);

        Task<Order?> GetByIdAsync(int id);
        Task<List<Order>> GetAllActiveOrdersAsync();
        Task<List<Order>> GetScheduledForUserAsync(string userId);
        Task<List<Order>> GetActiveForUserAsync(string userId);
        Task<List<Order>> GetCompletedForUserAsync(string userId);
        Task<List<Order>> GetCancelledForUserAsync(string userId);
    }
}
