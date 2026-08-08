using System.Collections.Generic;

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
        Task<List<string>> GetAllDriverTokensAsync(CancellationToken ct);
        Task<List<string>> GetTargetDriverTokensAsync(IReadOnlyList<int> driverIds, CancellationToken ct);
    }
}
