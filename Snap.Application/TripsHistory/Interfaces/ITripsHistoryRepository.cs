using Snap.Application.Domain.Entities;
using Snap.Application.TripsHistory.DTOs;

namespace Snap.Application.TripsHistory.Interfaces
{
    public interface ITripsHistoryRepository
    {
        void Add(Domain.Entities.TripsHistory trip);
        Task<List<Domain.Entities.TripsHistory>> GetAllAsync();
        Task<Domain.Entities.TripsHistory?> GetByIdAsync(int id);
        Task<Driver?> FindDriverByIdOrUserIdAsync(string driverIdOrUserId);
        Task<List<DriverTripHistoryDetailsDto>> GetDriverTripHistoryDetailsAsync(int driverId);
    }
}
