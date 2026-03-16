using Snap.APIs.DTOs;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Snap.APIs.Services
{
    public interface IDriverLocationService
    {
        void UpdateLocation(int driverId, string driverName, double lat, double lng);
        void RemoveDriver(int driverId);
        List<DriverLocationResponseDto> GetOnlineDrivers();
        DriverLocationResponseDto? GetDriverLocation(int driverId);
    }

    public class DriverLocationService : IDriverLocationService
    {
        // Thread-safe dictionary to store driver locations
        private readonly ConcurrentDictionary<int, DriverLocationResponseDto> _onlineDrivers = new();

        public void UpdateLocation(int driverId, string driverName, double lat, double lng)
        {
            var location = new DriverLocationResponseDto
            {
                DriverId = driverId,
                DriverName = driverName,
                Lat = lat,
                Lng = lng,
                LastUpdate = DateTime.UtcNow,
                IsOnline = true
            };

            _onlineDrivers.AddOrUpdate(driverId, location, (key, oldValue) => location);
        }

        public void RemoveDriver(int driverId)
        {
            _onlineDrivers.TryRemove(driverId, out _);
        }

        public List<DriverLocationResponseDto> GetOnlineDrivers()
        {
            // Filter out drivers who haven't updated in 5 minutes
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            return _onlineDrivers.Values
                .Where(d => d.IsOnline && d.LastUpdate > cutoff)
                .ToList();
        }

        public DriverLocationResponseDto? GetDriverLocation(int driverId)
        {
            if (_onlineDrivers.TryGetValue(driverId, out var location))
            {
                if ((DateTime.UtcNow - location.LastUpdate).TotalMinutes < 5)
                {
                    return location;
                }
            }
            return null;
        }
    }
}
