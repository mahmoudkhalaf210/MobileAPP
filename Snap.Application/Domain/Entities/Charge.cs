using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Domain.Entities
{
    public class Charge
    {
        [Key]
        public int Id { get; set; }
        public int DriverId { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public Driver Driver { get; set; }
    }
}
