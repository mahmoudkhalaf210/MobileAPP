using System;
using Snap.Application.Domain.Entities;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Models;

namespace Snap.Application.Orders.Interfaces
{
    // Persistence for the legacy (v1) Order aggregate. Status *transitions* are
    // deliberately excluded — those are atomic, concurrency-guarded operations that
    // live in IOrderWorkflowRepository. This interface covers plain reads, create,
    // tracked-mutate-then-save, and delete.
    public interface IOrderRepository
    {
        Task<OrderUserSnapshot?> GetUserSnapshotAsync(string userId);

        void Add(Order order);
        Task<Order?> FindTrackedAsync(int id);
        void Remove(Order order);

        Task<OrderDto?> GetByIdProjectedAsync(int id, CancellationToken ct = default);
        Task<List<OrderDto>> GetAllActiveProjectedAsync();
        Task<List<OrderDto>> GetScheduledForUserProjectedAsync(string userId);
        Task<OrderStatusSnapshot?> GetStatusSnapshotAsync(int id);

        Task<List<ScheduledOrderReminderInfo>> GetDueForReminderAsync(DateTime nowUtc, DateTime reminderThresholdUtc, CancellationToken ct);
        Task<List<ScheduledOrderStartingSoonInfo>> GetDueForStartingSoonAsync(DateTime startingSoonThresholdUtc, CancellationToken ct);

        Task<List<int>> GetBusyDriverIdsAsync(CancellationToken ct);

        Task<List<Order>> GetExpiredPendingTrackedAsync(DateTime cutoffUtc, CancellationToken ct);
        Task<int> DeleteExpiredPendingOrdersRawAsync(CancellationToken ct);
    }
}
