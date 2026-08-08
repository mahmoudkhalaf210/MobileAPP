using System;
using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Drivers.DTOs
{
    public class DriverLocationDto
    {
        [Required]
        public int DriverId { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Lat { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Lng { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
