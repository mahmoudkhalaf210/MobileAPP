using System;

namespace Snap.Application.Drivers.DTOs
{
    public class DriverLocationResponseDto
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsOnline { get; set; }

        // false while the driver has an active order (Approved..Started); reset to
        // true on the order's completion/cancellation.
        public bool IsAvailable { get; set; } = true;
    }
}
