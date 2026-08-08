using System;

namespace Snap.Application.Orders.Interfaces
{
    // Atomic, concurrency-guarded status transitions on Orders. Each method mirrors an
    // exact raw-SQL statement previously inlined in OrderService/ScheduledRideProcessorService —
    // moved verbatim, not redesigned, to preserve the existing race-condition-safety semantics.
    public interface IOrderWorkflowRepository
    {
        // UPDATE Orders SET Driverid = @driverId, Status = @newStatus WHERE Id = @orderId AND Status = @requiredCurrentStatus
        Task<int> TryAssignDriverAsync(int orderId, int driverId, string newStatus, string requiredCurrentStatus);

        // UPDATE Orders SET Status = @newStatus WHERE Id = @orderId AND Status = @requiredCurrentStatus
        Task<int> TryTransitionStatusAsync(int orderId, string newStatus, string requiredCurrentStatus);

        // Mirrors the EF.Functions.DateDiffMinute conflict-window check from AcceptScheduledOrderAsync.
        Task<bool> HasSchedulingConflictAsync(int driverId, DateTime referenceDate, int conflictWindowMinutes);
    }
}
