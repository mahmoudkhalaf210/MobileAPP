using System;

namespace Snap.Application.Domain.Entities
{
    // Ledger entry + idempotency guard: unique index on OrderId (see ConfigureUserPointsTransaction)
    // ensures a completed order can only ever award points once, even if the trigger fires more than once.
    public class UserPointsTransaction
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public int OrderId { get; set; }
        public int PointsAwarded { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
