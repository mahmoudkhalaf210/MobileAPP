using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Snap.APIs.DTOs
{
    public class DriverLocationDto
    {
        [Required]
        public int DriverId { get; set; }
        
        [Required]
        [Range(-90, 90, ErrorMessage = "Lat must be between -90 and 90")]
        public double Lat { get; set; }
        
        [Required]
        [Range(-180, 180, ErrorMessage = "Lng must be between -180 and 180")]
        public double Lng { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class LocationUpdateDto
    {
        [JsonPropertyName("lat")]

        public double Lat { get; set; }
        [JsonPropertyName("lng")]

        public double Lng { get; set; }
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DriverLocationResponseDto
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; } = null!;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsOnline { get; set; }
        /// <summary>
        /// False while the driver has an active order (Approved → Started).
        /// Reset to true when the trip completes or is cancelled.
        /// </summary>
        public bool IsAvailable { get; set; } = true;
    }
}