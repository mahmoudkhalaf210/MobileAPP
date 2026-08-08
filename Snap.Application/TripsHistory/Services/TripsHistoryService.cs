using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.TripsHistory.DTOs;
using Snap.Application.TripsHistory.Interfaces;

namespace Snap.Application.TripsHistory.Services
{
    public class TripsHistoryService : ITripsHistoryService
    {
        private readonly ITripsHistoryRepository _repo;
        private readonly IDriverRepository _driverRepo;
        private readonly IUnitOfWork _unitOfWork;

        public TripsHistoryService(ITripsHistoryRepository repo, IDriverRepository driverRepo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _driverRepo = driverRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<TripsHistoryDto> CreateTripsHistoryAsync(CreateTripsHistoryDto dto)
        {
            var driver = await _driverRepo.GetTrackedByIdAsync(dto.DriverId)
                ?? throw new KeyNotFoundException("Driver not found");

            var trip = new Domain.Entities.TripsHistory
            {
                Review = dto.Review,
                PaymentWay = dto.PaymentWay,
                From = dto.From,
                To = dto.To,
                Date = dto.Date,
                TotalTip = dto.TotalTip,
                DriverId = dto.DriverId
            };

            _repo.Add(trip);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(trip);
        }

        public async Task<List<TripsHistoryDto>> GetAllTripsHistoriesAsync() =>
            (await _repo.GetAllAsync()).Select(ToDto).ToList();

        public async Task<TripsHistoryDto> GetTripsHistoryByIdAsync(int id)
        {
            var trip = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Trip history not found");
            return ToDto(trip);
        }

        public async Task<List<DriverTripHistoryDetailsDto>> GetTripsHistoriesByUserIdAsync(string driverIdOrUserId)
        {
            var driver = await _repo.FindDriverByIdOrUserIdAsync(driverIdOrUserId)
                ?? throw new KeyNotFoundException("Driver not found");

            return await _repo.GetDriverTripHistoryDetailsAsync(driver.Id);
        }

        private static TripsHistoryDto ToDto(Domain.Entities.TripsHistory t) => new()
        {
            Id = t.Id,
            Review = t.Review,
            PaymentWay = t.PaymentWay,
            From = t.From,
            To = t.To,
            Date = t.Date,
            TotalTip = t.TotalTip,
            DriverId = t.DriverId
        };
    }
}
