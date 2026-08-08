using Snap.Application.TripsHistory.DTOs;

namespace Snap.Application.TripsHistory.Interfaces
{
    public interface ITripsHistoryService
    {
        Task<TripsHistoryDto> CreateTripsHistoryAsync(CreateTripsHistoryDto dto);
        Task<List<TripsHistoryDto>> GetAllTripsHistoriesAsync();
        Task<TripsHistoryDto> GetTripsHistoryByIdAsync(int id);
        Task<List<DriverTripHistoryDetailsDto>> GetTripsHistoriesByUserIdAsync(string driverIdOrUserId);
    }
}
