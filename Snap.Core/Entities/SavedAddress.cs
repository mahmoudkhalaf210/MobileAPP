using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snap.Core.Entities
{
    public class SavedAddress
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; } // Home / Work
        public string AddressLine { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UsageCount { get; set; } = 0;
    }
}
