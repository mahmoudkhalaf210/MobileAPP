namespace Snap.Application.Orders.DTOs
{
    public class CancelOrderByUserDto
    {
        public int OrderId { get; set; }
        public string UserId { get; set; }

        // Optional: Id of the CancelReason the user selected. Left null for
        // backward compatibility with clients that haven't adopted the reason picker yet.
        public int? CancelReasonId { get; set; }
    }
}
