using Snap.Application.Drivers.DTOs;

namespace Snap.Application.Drivers.Interfaces
{
    public interface ILocationService
    {
        Task<DriverLocationResponseDto> UpdateDriverLocationAsync(DriverLocationDto dto);
        Task<DriverLocationResponseDto> GetDriverLocationOrFallbackAsync(int driverId);
        Task RemoveDriverLocationAsync(int driverId);
    }
}
