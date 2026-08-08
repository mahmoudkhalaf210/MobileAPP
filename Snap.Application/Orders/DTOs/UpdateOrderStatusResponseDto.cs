namespace Snap.Application.Orders.DTOs
{
    public class UpdateOrderStatusResponseDto
    {
        public int OrderId { get; set; }
        public string? FCMToken { get; set; }
    }
}
