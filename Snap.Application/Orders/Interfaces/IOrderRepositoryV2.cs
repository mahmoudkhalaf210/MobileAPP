using Snap.Application.Domain.Entities;
using Snap.Application.Domain.Enums;

namespace Snap.Application.Orders.Interfaces
{
    // Read/insert-only for the v2 surface. Deliberately excludes Status mutation —
    // all order status transitions remain exclusively inside the existing, untouched
    // OrderService (now IOrderWorkflowRepository-backed), which owns the race-safe
    // atomic-update logic.
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
