using System.Collections.Concurrent;
using Snap.APIs.DTOs;

namespace Snap.APIs.Services
{
    public sealed class DriverLocationService : IDriverLocationService
    {
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

        // Primary cache: driverId → live location entry
        private readonly ConcurrentDictionary<int, DriverLocationResponseDto> _locations   = new();

        // Lock-free busy-set: drivers with an active order.
        // Outlives individual requests; seeded from DB on startup so reconnecting
        // drivers that have an active trip are still marked unavailable.
        private readonly ConcurrentDictionary<int, byte> _busyDrivers = new();

        public void UpdateLocation(int driverId, string driverName, double lat, double lng)
        {
            var isAvailableDefault = !_busyDrivers.ContainsKey(driverId);

            _locations.AddOrUpdate(
                driverId,
                _ => new DriverLocationResponseDto
                {
                    DriverId    = driverId,
                    DriverName  = driverName,
                    Lat         = lat,
                    Lng         = lng,
                    LastUpdate  = DateTime.UtcNow,
                    IsOnline    = true,
                    IsAvailable = isAvailableDefault
                },
                (_, existing) =>
                {
                    // A location ping must not override an explicit SetDriverAvailability call
                    existing.DriverName = driverName;
                    existing.Lat        = lat;
                    existing.Lng        = lng;
                    existing.LastUpdate = DateTime.UtcNow;
                    existing.IsOnline   = true;
                    return existing;
                });
        }

        public void RemoveDriver(int driverId) =>
            _locations.TryRemove(driverId, out _);

        public IReadOnlyList<DriverLocationResponseDto> GetConnectedDrivers() =>
            _locations.Values.ToList();

        public IReadOnlyList<DriverLocationResponseDto> GetOnlineDrivers()
        {
            var cutoff = DateTime.UtcNow - StaleThreshold;
            return _locations.Values
                .Where(d => d.IsOnline && d.IsAvailable && d.LastUpdate > cutoff)
                .ToList();
        }

        public DriverLocationResponseDto? GetDriverLocation(int driverId)
        {
            if (_locations.TryGetValue(driverId, out var loc) &&
                DateTime.UtcNow - loc.LastUpdate < StaleThreshold)
                return loc;
            return null;
        }

        public void SetDriverAvailability(int driverId, bool isAvailable)
        {
            if (isAvailable)
                _busyDrivers.TryRemove(driverId, out _);
            else
                _busyDrivers[driverId] = 0;

            if (_locations.TryGetValue(driverId, out var loc))
                loc.IsAvailable = isAvailable;
        }

        public void PreloadUnavailableDrivers(IReadOnlySet<int> busyDriverIds)
        {
            foreach (var id in busyDriverIds)
            {
                _busyDrivers[id] = 0;
                if (_locations.TryGetValue(id, out var loc))
                    loc.IsAvailable = false;
            }
        }
    }
}
