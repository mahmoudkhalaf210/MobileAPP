using Snap.APIs.DTOs;
using System.Collections.Concurrent;

namespace Snap.APIs.Services
{
    public interface IDriverLocationService
    {
        void UpdateLocation(int driverId, string driverName, double lat, double lng);
        void RemoveDriver(int driverId);

        /// <summary>
        /// Returns drivers that are: online, available (no active order), and whose
        /// location was refreshed within the last 5 minutes.
        /// </summary>
        List<DriverLocationResponseDto> GetOnlineDrivers();

        DriverLocationResponseDto? GetDriverLocation(int driverId);

        /// <summary>
        /// Marks the driver available or unavailable for new orders.
        /// Also updates the persistent busy-set so reconnecting drivers inherit
        /// the correct state after a server restart.
        /// </summary>
        void SetDriverAvailability(int driverId, bool isAvailable);

        /// <summary>
        /// Called once at startup by <see cref="DriverAvailabilityInitializer"/>.
        /// Seeds the busy-set from DB so drivers that reconnect after a restart
        /// are still marked unavailable while their active trip is in progress.
        /// </summary>
        void PreloadUnavailableDrivers(IReadOnlySet<int> busyDriverIds);
    }

    public sealed class DriverLocationService : IDriverLocationService
    {
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

        private readonly ConcurrentDictionary<int, DriverLocationResponseDto> _locations = new();

        // ConcurrentDictionary<int,byte> as a lock-free set for busy driver IDs.
        // Outlives individual HTTP requests — updated by OrderService on every
        // status transition that changes availability.
        private readonly ConcurrentDictionary<int, byte> _busyDrivers = new();

        // ── IDriverLocationService ────────────────────────────────────────────────

        public void PreloadUnavailableDrivers(IReadOnlySet<int> busyDriverIds)
        {
            foreach (var id in busyDriverIds)
            {
                _busyDrivers[id] = 0;
                // If the driver is already in the location cache, update in place
                if (_locations.TryGetValue(id, out var loc))
                    loc.IsAvailable = false;
            }
        }

        public void UpdateLocation(int driverId, string driverName, double lat, double lng)
        {
            // Drivers that were busy before restart stay unavailable on reconnect
            var availableByDefault = !_busyDrivers.ContainsKey(driverId);

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
                    IsAvailable = availableByDefault
                },
                (_, existing) =>
                {
                    // Preserve IsAvailable — a location ping must not override
                    // an explicit SetDriverAvailability(false) call
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

        public List<DriverLocationResponseDto> GetOnlineDrivers()
        {
            var cutoff = DateTime.UtcNow - StaleThreshold;
            return _locations.Values
                .Where(d => d.IsOnline && d.IsAvailable && d.LastUpdate > cutoff)
                .ToList();
        }

        public DriverLocationResponseDto? GetDriverLocation(int driverId)
        {
            if (_locations.TryGetValue(driverId, out var loc)
                && DateTime.UtcNow - loc.LastUpdate < StaleThreshold)
                return loc;
            return null;
        }

        public void SetDriverAvailability(int driverId, bool isAvailable)
        {
            if (isAvailable)
                _busyDrivers.TryRemove(driverId, out _);  // remove from persistent busy-set
            else
                _busyDrivers[driverId] = 0;               // add to persistent busy-set

            if (_locations.TryGetValue(driverId, out var loc))
                loc.IsAvailable = isAvailable;
            // If the driver is offline the busy-set change still persists,
            // so UpdateLocation will use the correct state when they reconnect.
        }
    }
}
