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
        /// Uses SQL Server's GETDATE() to ensure time comparison is done using database server time.
        /// </summary>
        private async Task DeleteExpiredPendingOrdersAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SnapDbContext>();

            try
            {
                // Use raw SQL with GETDATE() to ensure time comparison uses database server time
                // This matches the SQL query: DELETE FROM Orders WHERE Status = 'pending' AND Date < DATEADD(MINUTE, -4, GETDATE())
                var sqlQuery = $@"
                    delete from Orders
                    WHERE Status = 'pending' 
                      AND Date AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time' 
                          <= FORMAT(DATEADD(MINUTE, -4, 
                              (GETDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time')
                          ), 'yyyy-MM-dd HH:mm')
                   ";

                var deletedCount = await context.Database.ExecuteSqlRawAsync(sqlQuery, cancellationToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Successfully deleted {DeletedCount} expired pending order(s) using database server time (GETDATE()).",
                        deletedCount);
                }
                else
                {
                    _logger.LogDebug("No expired pending orders found.");
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

