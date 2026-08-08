namespace Snap.Application.Orders.Models
{
    // Minimal user projection needed by CreateOrderAsync (avoids materialising a full User entity).
    public class OrderUserSnapshot
    {
        public string? Image { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
