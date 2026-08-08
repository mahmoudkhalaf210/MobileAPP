using Microsoft.Extensions.Options;
using Snap.Application.Points.Interfaces;
using Snap.Application.Points.Settings;

namespace Snap.Application.Points.Services
{
    public class PointsService : IPointsService
    {
        private readonly IUserPointsRepository _repo;
        private readonly IOptions<PointsSettings> _options;

        public PointsService(IUserPointsRepository repo, IOptions<PointsSettings> options)
        {
            _repo = repo;
            _options = options;
        }

        public Task<int> GetBalanceAsync(string userId) => _repo.GetBalanceAsync(userId);

        public Task AwardForCompletedOrderAsync(int orderId, string userId) =>
            _repo.TryAwardPointsForOrderAsync(orderId, userId, _options.Value.PointsPerCompletedOrder);
    }
}
