using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snap.Repository.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Snap.APIs.Services
{
    /// <summary>
    /// Background service that periodically checks for and deletes expired pending orders.
    /// Follows the outbox pattern approach - runs every minute to process pending deletions.
    /// </summary>
    public class PendingOrderDeletionService : BackgroundService
    {
        private readonly ILogger<PendingOrderDeletionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private const int ExpirationMinutes = 4;
        private const int CheckIntervalMinutes = 1;

        public PendingOrderDeletionService(
            ILogger<PendingOrderDeletionService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PendingOrderDeletionService is starting. Checking every {Interval} minute(s) for orders older than {Expiration} minutes.", 
                CheckIntervalMinutes, ExpirationMinutes);

            // Wait a bit before first execution to allow application to fully start
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DeleteExpiredPendingOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while deleting expired pending orders.");
                }

                // Wait for the specified interval before next check
                await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
            }

            _logger.LogInformation("PendingOrderDeletionService is stopping.");
        }

        /// <summary>
        /// Deletes pending orders that are older than the expiration time.
        /// </summary>
        private async Task DeleteExpiredPendingOrdersAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SnapDbContext>();

            try
            {
                var expirationThreshold = DateTime.UtcNow.AddMinutes(-ExpirationMinutes);

                // Find all pending orders older than the expiration threshold
                var expiredOrders = await context.Orders
                    .Where(o => o.Status == "pending" && o.Date < expirationThreshold)
                    .Select(o => new { o.Id, o.Date })
                    .ToListAsync(cancellationToken);

                if (!expiredOrders.Any())
                {
                    _logger.LogDebug("No expired pending orders found.");
                    return;
                }

                _logger.LogInformation("Found {Count} expired pending order(s) to delete.", expiredOrders.Count);

                // Delete expired orders
                var orderIds = expiredOrders.Select(o => o.Id).ToList();
                var deletedCount = await context.Orders
                    .Where(o => orderIds.Contains(o.Id) && o.Status == "pending")
                    .ExecuteDeleteAsync(cancellationToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Successfully deleted {DeletedCount} expired pending order(s). Order IDs: {OrderIds}",
                        deletedCount,
                        string.Join(", ", orderIds));
                }
                else
                {
                    _logger.LogWarning(
                        "Expected to delete {ExpectedCount} orders, but {DeletedCount} were actually deleted. Orders may have been updated by another process.",
                        expiredOrders.Count,
                        deletedCount);
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error occurred while deleting expired pending orders.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while deleting expired pending orders.");
                throw;
            }
        }
    }
}

