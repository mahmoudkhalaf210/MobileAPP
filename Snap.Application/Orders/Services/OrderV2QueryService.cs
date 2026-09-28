using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Orders.Mapping;

namespace Snap.Application.Orders.Services
{
    public class OrderV2QueryService : IOrderV2QueryService
    {
        private readonly IOrderRepositoryV2 _repo;

        public OrderV2QueryService(IOrderRepositoryV2 repo)
        {
            _repo = repo;
        }

        public async Task<List<OrderV2Dto>> GetAllOrdersAsync(int? driverId = null) =>
            (await _repo.GetAllActiveOrdersAsync(driverId)).Select(OrderV2Mapper.ToDto).ToList();

        public async Task<OrderV2Dto?> GetByIdAsync(int id)
        {
            var order = await _repo.GetByIdAsync(id);
            return order is null ? null : OrderV2Mapper.ToDto(order);
        }

        public async Task<List<OrderV2Dto>> GetScheduledForUserAsync(string userId) =>
            (await _repo.GetScheduledForUserAsync(userId)).Select(OrderV2Mapper.ToDto).ToList();

        public async Task<List<OrderV2Dto>> GetActiveForUserAsync(string userId) =>
            (await _repo.GetActiveForUserAsync(userId)).Select(OrderV2Mapper.ToDto).ToList();

        public async Task<List<OrderV2Dto>> GetCompletedForUserAsync(string userId) =>
            (await _repo.GetCompletedForUserAsync(userId)).Select(OrderV2Mapper.ToDto).ToList();

        public async Task<List<OrderV2Dto>> GetCancelledForUserAsync(string userId) =>
            (await _repo.GetCancelledForUserAsync(userId)).Select(OrderV2Mapper.ToDto).ToList();

        public async Task<List<OrderV2Dto>> GetAllScheduledOrdersAsync(int? driverId = null) =>
            (await _repo.GetAllScheduledOrdersAsync(driverId)).Select(OrderV2Mapper.ToDto).ToList();

        public async Task<OrderV2Dto?> GetScheduledByIdAsync(int id)
        {
            var order = await _repo.GetScheduledByIdAsync(id);
            return order is null ? null : OrderV2Mapper.ToDto(order);
        }

        public async Task<List<OrderV2Dto>> GetScheduledForDriverAsync(int driverId) =>
            (await _repo.GetScheduledForDriverAsync(driverId)).Select(OrderV2Mapper.ToDto).ToList();
    }
}
