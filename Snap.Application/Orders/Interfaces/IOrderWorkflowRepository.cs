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

        // True if the driver already has a live, in-progress order (approve/Arrived/Started).
        Task<bool> HasActiveOrderAsync(int driverId);

        // True if the driver's linked account is registered as female (Pink Mode eligibility).
        Task<bool> IsDriverFemaleAsync(int driverId);

        // True if the driver's car type (CarData.CarBrand) matches the order's CarType.
        Task<bool> DriverMatchesCarTypeAsync(int driverId, string? orderCarType);
    }
}
