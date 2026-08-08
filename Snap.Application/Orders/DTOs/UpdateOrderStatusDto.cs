namespace Snap.Application.Orders.DTOs
{
    public class UpdateOrderStatusDto
    {
        public int OrderId { get; set; }
        public int? Driverid { get; set; }
        public string? FCMToken { get; set; }
    }
}
