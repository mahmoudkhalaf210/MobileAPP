using Snap.Business.DTOs;

namespace Snap.Business.Interfaces
{
    // Declared here so OrdersV2Controller (Snap.APIs) can depend on an abstraction
    // owned by the inward layer. The implementation must live in Snap.APIs, since it
    // composes over the existing Snap.APIs.Services.IOrderService — Snap.Business
    // cannot reference Snap.APIs without creating a cycle.
    public interface IOrderV2CommandService
    {
        Task<OrderV2Dto> CreateNormalAsync(CreateNormalOrderV2Dto dto);
        Task<OrderV2Dto> CreateScheduledAsync(CreateScheduledOrderV2Dto dto);
    }
}
