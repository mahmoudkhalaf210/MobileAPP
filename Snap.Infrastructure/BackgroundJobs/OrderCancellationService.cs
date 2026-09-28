using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Orders.Mapping;

namespace Snap.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Runs every minute:
    ///   1. Pending orders no driver accepted within <see cref="PendingExpirationMinutes"/>
    ///      are cancelled (atomically, so a driver accepting at the same moment wins)
    ///      and the user + drivers are notified.
    ///   2. Every cancelled order — expired, user/driver cancelled, or unclaimed
    ///      scheduled — is soft-deleted (hidden from the apps, kept in the DB).
    /// All comparisons are in UTC, matching how Order.Date is stored.
    /// </summary>
    public class OrderCancellationService : BackgroundService
    {
        private const int PendingExpirationMinutes = 10;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        private readonly ILogger<OrderCancellationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public OrderCancellationService(
            ILogger<OrderCancellationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderCancellationService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CancelExpiredPendingOrdersAsync(stoppingToken);
                    await SoftDeleteCancelledOrdersAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up expired/cancelled orders.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.LogInformation("OrderCancellationService is stopping.");
        }

        private async Task CancelExpiredPendingOrdersAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var workflowRepo = scope.ServiceProvider.GetRequiredService<IOrderWorkflowRepository>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotificationService>();

            var cutoffUtc = DateTime.UtcNow.AddMinutes(-PendingExpirationMinutes);
            var expiredOrders = await orderRepo.GetExpiredPendingTrackedAsync(cutoffUtc, ct);

            var cancelledStatus = OrderStatus.Cancel.GetStringValue();
            var pendingStatus = OrderStatus.Pending.GetStringValue();

            foreach (var order in expiredOrders)
            {
                // Conditional update: skipped if a driver accepted it since we read it.
                var rows = await workflowRepo.TryTransitionStatusAsync(order.Id, cancelledStatus, pendingStatus);
                if (rows == 0)
                    continue;

                order.Status = cancelledStatus;
                _logger.LogInformation("Order {OrderId} auto-cancelled: no driver accepted within {Minutes} minutes.",
                    order.Id, PendingExpirationMinutes);

                try
                {
                    await notifier.NotifyOrderCancelledAsync(
                        order.Id,
                        OrderMapper.ToDto(order),
                        "لم نتمكن من إيجاد كابتن لطلبك حالياً، برجاء المحاولة مرة أخرى.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify about auto-cancelled order {OrderId}", order.Id);
                }
            }
        }

        private async Task SoftDeleteCancelledOrdersAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            var deleted = await orderRepo.SoftDeleteCancelledOrdersAsync(ct);
            if (deleted > 0)
                _logger.LogInformation("Soft-deleted {Count} cancelled order(s).", deleted);
        }
    }
}
