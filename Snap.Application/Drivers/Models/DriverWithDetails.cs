using Snap.Application.Domain.Entities;

namespace Snap.Application.Drivers.Models
{
    // Composite projection mirroring the single EF query (with FirstOrDefault subqueries)
    // that DriverController.GetApprovedDrivers used to run inline.
    public class DriverWithDetails
    {
        public Driver Driver { get; set; } = null!;
        public User? User { get; set; }
        public CarData? CarData { get; set; }
    }
}
