using System;

namespace Snap.Application.Orders.Models
{
    // Minimal projection used by AcceptScheduledOrderAsync's pre-check.
    public class OrderStatusSnapshot
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        public DateTime Date { get; set; }
    }
}
