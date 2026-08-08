using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snap.Application.Common.Interfaces.Notifications;
using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.Interfaces;

namespace Snap.Infrastructure.BackgroundJobs
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
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notifRepo = scope.ServiceProvider.GetRequiredService<IOrderNotificationRepository>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var fourMinutesAgo = DateTime.UtcNow.AddMinutes(-4);

            // Find all pending orders older than 4 minutes
            var expiredOrders = await orderRepo.GetExpiredPendingTrackedAsync(fourMinutesAgo, stoppingToken);

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
                            token = await notifRepo.GetUserFcmTokenAsync(order.UserId);
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

                await unitOfWork.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
