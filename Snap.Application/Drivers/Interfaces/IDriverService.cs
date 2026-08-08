using Snap.Application.Domain.Entities;
using Snap.Application.Drivers.DTOs;

namespace Snap.Application.Drivers.Interfaces
{
    public interface IDriverService
    {
        Task<CreateDriver> CreateDriverAsync(CreateDriver dto);
        Task<DriverDto> GetDriverByUserIdAsync(string userId);
        Task<List<PendingDto>> GetPendingDriversAsync();
        Task ChangeDriverStatusAsync(int driverId, ChangeDriverStatusDto dto);
        Task AddReviewAsync(int id, AddDriverReviewDto dto);
        Task<double> GetDriverReviewAsync(int id);
        Task<Charge> RequestChargeAsync(RequestChargeDto dto);
        Task<List<Charge>> GetChargesAsync();
        Task<string> HandleChargeAsync(int id, ChargeActionDto dto);
        Task<double> DeductFromWalletAsync(DeductWalletDto dto);
        Task<DriverIdDto> GetDriverByDriverIdAsync(int id);
        Task<List<ApprovedDriverWithCarDto>> GetApprovedDriversAsync();
    }
}
