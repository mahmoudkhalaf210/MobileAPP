namespace Snap.Application.Drivers.DTOs
{
    public class DriverIdDto
    {
        public int Id { get; set; }
        public string DriverPhoto { get; set; }
        public string DriverFullname { get; set; }
        public string Email { get; set; }
        public string UserId { get; set; }
        public double Review { get; set; }
        public int? PhoneNumber { get; set; }
        public string Gender { get; set; } // Added
        public string CarBrand { get; set; } // Added
    }
}
