using Snap.Application.Drivers.DTOs;

namespace Snap.Application.Drivers.Interfaces
{
    public interface IDriverLocationService
    {
        /// <summary>Upserts the driver's location in the in-memory cache.</summary>
        void UpdateLocation(int driverId, string driverName, double lat, double lng);

        /// <summary>Removes the driver from the cache (called on disconnect).</summary>
        void RemoveDriver(int driverId);

        /// <summary>
        /// All drivers currently in the cache (connected).
        /// Used for map display — includes busy/unavailable drivers.
        /// </summary>
        IReadOnlyList<DriverLocationResponseDto> GetConnectedDrivers();

        /// <summary>
        /// Drivers that are online, available for a new order, and whose
        /// location was refreshed within the last 5 minutes.
        /// Used by OrderService to select dispatch candidates.
        /// </summary>
        IReadOnlyList<DriverLocationResponseDto> GetOnlineDrivers();

        DriverLocationResponseDto? GetDriverLocation(int driverId);

        /// <summary>
        /// Marks the driver available or unavailable for new orders.
        /// Persists across reconnects via the busy-set.
        /// </summary>
        void SetDriverAvailability(int driverId, bool isAvailable);

        /// <summary>
        /// Called once at startup by DriverAvailabilityInitializer
        /// to seed the busy-set from DB so drivers with active orders stay
        /// unavailable after a server restart.
        /// </summary>
        void PreloadUnavailableDrivers(IReadOnlySet<int> busyDriverIds);
    }
}
