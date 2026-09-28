using Microsoft.Extensions.Options;
using Snap.Application.Points.Interfaces;
using Snap.Application.Points.Settings;

namespace Snap.Application.Points.Services
{
    public class DriverPointsService : IDriverPointsService
    {
        private readonly IDriverPointsRepository _repo;
        private readonly IOptions<DriverPointsSettings> _options;

        public DriverPointsService(IDriverPointsRepository repo, IOptions<DriverPointsSettings> options)
        {
            _repo = repo;
            _options = options;
        }

        public Task<int> GetBalanceAsync(int driverId) => _repo.GetBalanceAsync(driverId);

        public Task AwardForCompletedOrderAsync(int orderId, int driverId) =>
            _repo.TryAwardPointsForOrderAsync(orderId, driverId, _options.Value.PointsPerCompletedOrder);
    }
}
