using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snap.Core.Entities;
using Snap.Core.Services;
using Snap.Repository.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Snap.APIs.Services
{
    public class OrderCancellationService : BackgroundService
    {
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
                    await CancelExpiredOrders(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while canceling expired orders.");
                }

                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("OrderCancellationService is stopping.");
        }

        private async Task CancelExpiredOrders(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SnapDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var fourMinutesAgo = DateTime.UtcNow.AddMinutes(-4);
            var pendingStatus = OrderStatus.Pending.GetStringValue();

            // Find all pending orders older than 4 minutes
            var expiredOrders = await context.Orders
                .Where(o => o.Status == pendingStatus && o.Date < fourMinutesAgo)
                .ToListAsync(stoppingToken);

            if (expiredOrders.Any())
            {
                _logger.LogInformation($"Found {expiredOrders.Count} expired pending orders to cancel.");

                foreach (var order in expiredOrders)
                {
                    order.Status = OrderStatus.Cancel.GetStringValue();
                    _logger.LogInformation($"Order {order.Id} has been automatically cancelled due to timeout.");

                    // Notify User
                    try 
                    {
                        // Get user token if not in order
                        string? token = order.FCMToken;
                        if (string.IsNullOrEmpty(token))
                        {
                            var userToken = await context.FCMTokenUsers
                                .Where(t => t.UserId == order.UserId)
                                .Select(t => t.Token)
                                .FirstOrDefaultAsync(stoppingToken);
                            token = userToken;
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            await notificationService.SendNotification(token, "Order Cancelled", "We could not find a driver for your order at this time.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to notify user for order {order.Id}");
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
