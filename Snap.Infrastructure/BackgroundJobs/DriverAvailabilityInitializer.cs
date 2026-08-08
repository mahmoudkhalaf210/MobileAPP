using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Orders.Interfaces;

namespace Snap.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Runs once at startup to re-hydrate in-memory driver availability from the DB.
    ///
    /// Problem without this:
    ///   After a server restart all in-memory state is lost.  Drivers with an active
    ///   order (Approved / Arrived / Started) reconnect and appear as IsAvailable=true,
    ///   so new orders get dispatched to them — causing double-booking.
    ///
    /// Solution:
    ///   Query Orders for any active assignment, tell DriverLocationService to treat
    ///   those driver IDs as unavailable when they reconnect (UpdateLocation).
    ///   When the trip resolves (Complete / Cancel), OrderService calls
    ///   SetDriverAvailability(id, true) which also removes them from the busy set.
    /// </summary>
    public sealed class DriverAvailabilityInitializer : IHostedService
    {
        private readonly IServiceScopeFactory   _scopeFactory;
        private readonly IDriverLocationService _locationService;
        private readonly ILogger<DriverAvailabilityInitializer> _logger;

        public DriverAvailabilityInitializer(
            IServiceScopeFactory            scopeFactory,
            IDriverLocationService           locationService,
            ILogger<DriverAvailabilityInitializer> logger)
        {
            _scopeFactory    = scopeFactory;
            _locationService = locationService;
            _logger          = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope    = _scopeFactory.CreateScope();
            var orderRepo      = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            var busyList      = await orderRepo.GetBusyDriverIdsAsync(cancellationToken);
            var busyDriverIds = busyList.ToHashSet();

            if (busyDriverIds.Count == 0)
            {
                _logger.LogInformation("DriverAvailabilityInitializer: no active orders — all drivers start available");
                return;
            }

            _locationService.PreloadUnavailableDrivers(busyDriverIds);

            _logger.LogInformation(
                "DriverAvailabilityInitializer: seeded {Count} busy driver(s) as unavailable: [{Ids}]",
                busyDriverIds.Count,
                string.Join(", ", busyDriverIds));
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
