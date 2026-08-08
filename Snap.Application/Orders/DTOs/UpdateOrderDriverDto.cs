namespace Snap.Application.Orders.DTOs
{
    public class UpdateOrderDriverDto
    {
        public int OrderId { get; set; }
        public int Driverid { get; set; }
        public string Status { get; set; }
        public string? FCMToken { get; set; }
    }
}
