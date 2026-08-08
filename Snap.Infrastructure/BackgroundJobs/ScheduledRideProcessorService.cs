using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Orders.Settings;
using System.Collections.Concurrent;
using System.Globalization;

namespace Snap.Infrastructure.BackgroundJobs
{
    public sealed class ScheduledRideProcessorService : BackgroundService
    {
        private readonly ILogger<ScheduledRideProcessorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDriverLocationService _locationService;
        private readonly IOptions<OrderSettings> _options;

        private static readonly ConcurrentDictionary<int, byte> _reminded = new();

        public ScheduledRideProcessorService(
            ILogger<ScheduledRideProcessorService> logger,
            IServiceProvider serviceProvider,
            IDriverLocationService locationService,
            IOptions<OrderSettings> options)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _locationService = locationService;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ScheduledRideProcessorService failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessOnceAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var workflowRepo = scope.ServiceProvider.GetRequiredService<IOrderWorkflowRepository>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotificationService>();

            var nowUtc = DateTime.UtcNow;
            var settings = _options.Value;

            var reminderMinutes = Math.Max(0, settings.ScheduledReminderMinutes);
            var startingSoonMinutes = Math.Max(0, settings.ScheduledStartingSoonMinutes);

            var reminderThreshold = nowUtc.AddMinutes(reminderMinutes);
            var startingSoonThreshold = nowUtc.AddMinutes(startingSoonMinutes);

            var dueForReminder = await orderRepo.GetDueForReminderAsync(nowUtc, reminderThreshold, ct);

            foreach (var o in dueForReminder)
            {
                if (!_reminded.TryAdd(o.Id, 0))
                    continue;

                await notifier.NotifyScheduledRideReminderAsync(o.Id, o.DriverId, o.Date.ToUniversalTime());
            }

            var dueForStartingSoon = await orderRepo.GetDueForStartingSoonAsync(startingSoonThreshold, ct);

            foreach (var o in dueForStartingSoon)
            {
                var rows = await workflowRepo.TryTransitionStatusAsync(o.Id, "pending", "scheduled_accepted");

                if (rows == 0)
                    continue;

                _locationService.SetDriverAvailability(o.DriverId, isAvailable: false);

                var orderDto = await orderRepo.GetByIdProjectedAsync(o.Id, ct);

                if (orderDto == null)
                    continue;

                await notifier.NotifyScheduledRideStartingSoonAsync(o.Id, o.DriverId, orderDto);
            }

            var remindedCount = dueForReminder.Count;
            var startingSoonCount = dueForStartingSoon.Count;

            if (remindedCount > 0 || startingSoonCount > 0)
            {
                _logger.LogInformation(
                    "ScheduledRideProcessorService cycle: reminders={Reminders}, startingSoon={StartingSoon}, nowUtc={Now}",
                    remindedCount,
                    startingSoonCount,
                    nowUtc.ToString("O", CultureInfo.InvariantCulture));
            }
        }
    }
}
