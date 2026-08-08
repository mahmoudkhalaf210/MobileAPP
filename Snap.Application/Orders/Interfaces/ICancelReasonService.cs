using Snap.Application.Orders.DTOs;

namespace Snap.Application.Orders.Interfaces
{
    public interface ICancelReasonService
    {
        Task<CancelReasonDto> CreateAsync(CreateCancelReasonDto dto);
        Task<List<CancelReasonDto>> GetAllAsync();
    }
}
