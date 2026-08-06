using Microsoft.Extensions.Options;
using Snap.Business.Interfaces;
using Snap.Business.Settings;
using Snap.DataAccess.Interfaces;

namespace Snap.Business.Services
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
