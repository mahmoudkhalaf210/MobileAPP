using System;
using System.Text.Json.Serialization;

namespace Snap.Application.Drivers.DTOs
{
    public class LocationUpdateDto
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
