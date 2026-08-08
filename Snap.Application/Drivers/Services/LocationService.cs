using Snap.Application.Drivers.DTOs;
using Snap.Application.Drivers.Interfaces;

namespace Snap.Application.Drivers.Services
{
    public class LocationService : ILocationService
    {
        private readonly IDriverRepository _driverRepo;
        private readonly IDriverLocationService _locationService;
        private readonly ILocationRealtimeNotifier _realtime;

        public LocationService(
            IDriverRepository driverRepo,
            IDriverLocationService locationService,
            ILocationRealtimeNotifier realtime)
        {
            _driverRepo = driverRepo;
            _locationService = locationService;
            _realtime = realtime;
        }

        public async Task<DriverLocationResponseDto> UpdateDriverLocationAsync(DriverLocationDto dto)
        {
            var driverName = await _driverRepo.GetDriverNameByIdAsync(dto.DriverId)
                ?? throw new KeyNotFoundException("Driver not found");

            _locationService.UpdateLocation(dto.DriverId, driverName, dto.Lat, dto.Lng);

            var updated = _locationService.GetDriverLocation(dto.DriverId)!;
            await _realtime.BroadcastLocationEventAsync("LocationUpdate", updated);

            return updated;
        }

        public async Task<DriverLocationResponseDto> GetDriverLocationOrFallbackAsync(int driverId)
        {
            var cached = _locationService.GetDriverLocation(driverId);
            if (cached != null)
                return cached;

            var driverName = await _driverRepo.GetDriverNameByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Driver not found");

            return new DriverLocationResponseDto
            {
                DriverId = driverId,
                DriverName = driverName,
                IsOnline = false
            };
        }

        public async Task RemoveDriverLocationAsync(int driverId)
        {
            var snapshot = _locationService.GetDriverLocation(driverId);
            _locationService.RemoveDriver(driverId);

            if (snapshot != null)
            {
                snapshot.IsOnline = false;
                await _realtime.BroadcastLocationEventAsync("DriverRemoved", new { driverId });
            }
        }
    }
}
