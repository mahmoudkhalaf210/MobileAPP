using System;

namespace Snap.Application.Orders.Models
{
    // Minimal projection used by the accept pre-checks (status/pink-mode/car type).
    public class OrderStatusSnapshot
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        public DateTime Date { get; set; }
        public bool PinkMode { get; set; }
        public string? CarType { get; set; }
    }
}
