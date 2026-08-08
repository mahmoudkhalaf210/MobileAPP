namespace Snap.Application.Domain.Enums
{
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
                OrderStatus.Started => "Started",
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
                "started" => OrderStatus.Started,
                "complete" => OrderStatus.Complete,
                _ => OrderStatus.Pending
            };
        }
    }
}
