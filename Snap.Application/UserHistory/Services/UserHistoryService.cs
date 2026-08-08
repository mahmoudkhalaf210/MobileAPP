using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.UserHistory.DTOs;
using Snap.Application.UserHistory.Interfaces;

namespace Snap.Application.UserHistory.Services
{
    public class UserHistoryService : IUserHistoryService
    {
        private readonly IUserHistoryRepository _repo;
        private readonly IUnitOfWork _unitOfWork;

        public UserHistoryService(IUserHistoryRepository repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<UserHistoryDto> CreateUserHistoryAsync(CreateUserHistoryDto dto)
        {
            var user = await _repo.GetUserByIdAsync(dto.UserId)
                ?? throw new KeyNotFoundException("User not found");

            var history = new Domain.Entities.UserHistory
            {
                UserId = dto.UserId,
                From = dto.From,
                To = dto.To,
                Price = dto.Price,
                Date = dto.Date,
                PaymentMethod = dto.PaymentMethod,
                RideType = dto.RideType
            };

            _repo.Add(history);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(history);
        }

        public async Task<List<UserHistoryDto>> GetAllUserHistoriesAsync() =>
            (await _repo.GetAllAsync()).Select(ToDto).ToList();

        public async Task<List<UserHistoryDetailsDto>> GetUserHistoriesByUserIdAsync(string userId)
        {
            var histories = await _repo.GetUserHistoryDetailsAsync(userId);
            if (histories == null || histories.Count == 0)
                throw new KeyNotFoundException("No trips found for this userId");

            return histories;
        }

        public async Task<UserHistoryDto> GetUserHistoryByIdAsync(int id)
        {
            var history = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("User history not found");
            return ToDto(history);
        }

        private static UserHistoryDto ToDto(Domain.Entities.UserHistory h) => new()
        {
            Id = h.Id,
            UserId = h.UserId,
            From = h.From,
            To = h.To,
            Price = h.Price,
            Date = h.Date,
            PaymentMethod = h.PaymentMethod,
            RideType = h.RideType
        };
    }
}
