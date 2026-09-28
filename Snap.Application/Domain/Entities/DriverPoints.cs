using System;

namespace Snap.Application.Domain.Entities
{
    public class DriverPoints
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public int Balance { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
