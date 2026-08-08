using System;

namespace Snap.Application.Domain.Entities
{
    public class ExplorePlace
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Base64 string, stored verbatim — mirrors User.Image (no file system involved).
        public string? Photo { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
