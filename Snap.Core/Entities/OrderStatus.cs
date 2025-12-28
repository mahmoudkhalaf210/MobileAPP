namespace Snap.Core.Entities
{
    public enum OrderStatus
    {
        Pending = 0,    // value: "pending"
        Approved = 1,   // value: "approve"
        Cancel = 2,     // value: "cancelled"
        Arrived = 3,    // value: "Arrived"
        Complete = 4    // value: "Complete"
    }

    public static class OrderStatusExtensions
    {
        public static string GetStringValue(this OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "pending",
                OrderStatus.Approved => "approve",
                OrderStatus.Cancel => "cancelled",
                OrderStatus.Arrived => "Arrived",
                OrderStatus.Complete => "Complete",
                _ => "pending"
            };
        }

        public static OrderStatus FromString(string status)
        {
            return status?.ToLower() switch
            {
                "pending" => OrderStatus.Pending,
                "approve" => OrderStatus.Approved,
                "cancelled" => OrderStatus.Cancel,
                "arrived" => OrderStatus.Arrived,
                "complete" => OrderStatus.Complete,
                _ => OrderStatus.Pending
            };
        }
    }
}

