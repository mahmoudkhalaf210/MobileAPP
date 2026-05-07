using Snap.APIs.DTOs;
using Snap.Core.Entities;

namespace Snap.APIs.Services
{
    public interface IOrderService
    {
        /// <summary>
        /// Creates the order, persists it, and enqueues driver notification as a
        /// background job. The controller receives the OrderDto immediately.
        /// </summary>
        Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);

        Task AcceptOrderAsync(UpdateOrderDriverDto dto);
        Task CancelOrderByDriverAsync(UpdateOrderDriverDto dto);
        Task HandleWorkflowTransitionAsync(UpdateOrderDriverDto dto, OrderStatus targetStatus);
        Task CancelOrderByUserAsync(CancelOrderByUserDto dto);

        Task<List<OrderDto>> GetAllOrdersAsync();
        Task<OrderDto> GetOrderByIdAsync(int id);
        Task DeleteOrderAsync(int id);
    }
}
