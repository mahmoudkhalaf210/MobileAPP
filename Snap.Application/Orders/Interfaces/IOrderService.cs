using Snap.Application.Domain.Enums;
using Snap.Application.Orders.DTOs;

namespace Snap.Application.Orders.Interfaces
{
    public interface IOrderService
    {
        Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
        Task AcceptOrderAsync(UpdateOrderDriverDto dto);
        Task AcceptScheduledOrderAsync(UpdateOrderDriverDto dto);
        Task CancelOrderByDriverAsync(UpdateOrderDriverDto dto);
        Task HandleWorkflowTransitionAsync(UpdateOrderDriverDto dto, OrderStatus targetStatus);
        Task CancelOrderByUserAsync(CancelOrderByUserDto dto);
        Task<List<OrderDto>> GetAllOrdersAsync();
        Task<List<OrderDto>> GetScheduledOrdersByUserAsync(string userId);
        Task<OrderDto> GetOrderByIdAsync(int id);
        Task DeleteOrderAsync(int id);
    }
}
