using Snap.Application.Orders.DTOs;

namespace Snap.Application.Orders.Interfaces
{
    public interface IOrderV2CommandService
    {
        Task<OrderV2Dto> CreateNormalAsync(CreateNormalOrderV2Dto dto);
        Task<OrderV2Dto> CreateScheduledAsync(CreateScheduledOrderV2Dto dto);
    }
}
