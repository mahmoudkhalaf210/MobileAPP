using System;

namespace Snap.Application.Domain.Entities
{
    // Ledger entry + idempotency guard: unique index on OrderId (see ConfigureDriverPointsTransaction)
    // ensures a completed order can only ever award driver points once, even if the trigger fires more than once.
    public class DriverPointsTransaction
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public int OrderId { get; set; }
        public int PointsAwarded { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
