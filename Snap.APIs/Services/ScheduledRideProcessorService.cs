using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snap.APIs.Mapping;
using Snap.APIs.Settings;
using Snap.Repository.Data;
using System.Collections.Concurrent;
using System.Globalization;

namespace Snap.APIs.Services
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
            var context = scope.ServiceProvider.GetRequiredService<SnapDbContext>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotificationService>();

            var nowUtc = DateTime.UtcNow;
            var settings = _options.Value;

            var reminderMinutes = Math.Max(0, settings.ScheduledReminderMinutes);
            var startingSoonMinutes = Math.Max(0, settings.ScheduledStartingSoonMinutes);

            var reminderThreshold = nowUtc.AddMinutes(reminderMinutes);
            var startingSoonThreshold = nowUtc.AddMinutes(startingSoonMinutes);

            var dueForReminder = await context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.Status == "scheduled_accepted" &&
                    o.Driverid != null &&
                    o.Date >= nowUtc &&
                    o.Date <= reminderThreshold)
                .Select(o => new { o.Id, DriverId = o.Driverid!.Value, o.Date })
                .ToListAsync(ct);

            foreach (var o in dueForReminder)
            {
                if (!_reminded.TryAdd(o.Id, 0))
                    continue;

                await notifier.NotifyScheduledRideReminderAsync(o.Id, o.DriverId, o.Date.ToUniversalTime());
            }

            var dueForStartingSoon = await context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.Status == "scheduled_accepted" &&
                    o.Driverid != null &&
                    o.Date <= startingSoonThreshold)
                .Select(o => new { o.Id, DriverId = o.Driverid!.Value })
                .ToListAsync(ct);

            foreach (var o in dueForStartingSoon)
            {
                var rows = await context.Database.ExecuteSqlRawAsync(
                    "UPDATE Orders SET Status = {0} WHERE Id = {1} AND Status = {2}",
                    "pending",
                    o.Id,
                    "scheduled_accepted");

                if (rows == 0)
                    continue;

                _locationService.SetDriverAvailability(o.DriverId, isAvailable: false);

                var orderDto = await context.Orders
                    .AsNoTracking()
                    .Where(x => x.Id == o.Id)
                    .Select(OrderMapper.ToProjection)
                    .FirstOrDefaultAsync(ct);

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

