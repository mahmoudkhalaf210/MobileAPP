using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Domain.Entities
{
    public class CarData
    {
        [Key]
        public int Id { get; set; }
        public string CarPhoto { get; set; }
        public string LicenseFront { get; set; }
        public string LicenseBack { get; set; }
        // Holds the car type (e.g. "Lada"/"Taxi"/"SuperMalaky") rather than a real
        // brand name — repurposed so order-notification filtering has a column to
        // match against without a schema change.
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public string CarColor { get; set; }
        public string PlateNumber { get; set; }
        // Foreign key to Driver
        public int DriverId { get; set; }
        public Driver Driver { get; set; }
    }
}
