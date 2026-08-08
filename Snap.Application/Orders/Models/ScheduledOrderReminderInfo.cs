using System;

namespace Snap.Application.Orders.Models
{
    public class ScheduledOrderReminderInfo
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public DateTime Date { get; set; }
    }
}
