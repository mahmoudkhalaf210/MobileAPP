using System.Collections.Generic;
using Snap.Application.Domain.Enums;

namespace Snap.Application.Orders.Interfaces
{
    // Driver-name and FCM-token lookups needed to build notification payloads.
    // Split out from IOrderRepository because it's a distinct responsibility
    // (notification data) used only by OrderNotificationService.
    public interface IOrderNotificationRepository
    {
        Task<string?> GetDriverNameAsync(int driverId);
        Task<string?> GetUserFcmTokenAsync(string userId);
        Task<string?> GetDriverFcmTokenAsync(int driverId);

        // carType: null = no car-type filtering (matches any). A driver who hasn't
        // set CarData.CarBrand yet is always included, regardless of carType.
        // femaleOnly: true = only drivers whose linked User.Gender is "female" (Pink Mode).
        Task<List<string>> GetAllDriverTokensAsync(CarType? carType, bool femaleOnly, CancellationToken ct);
        Task<List<string>> GetTargetDriverTokensAsync(IReadOnlyList<int> driverIds, CarType? carType, bool femaleOnly, CancellationToken ct);
    }
}
