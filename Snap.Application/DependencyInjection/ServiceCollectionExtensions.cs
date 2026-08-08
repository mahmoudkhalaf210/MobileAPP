using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snap.Application.AppVersions.Interfaces;
using Snap.Application.AppVersions.Services;
using Snap.Application.CarDatas.Interfaces;
using Snap.Application.CarDatas.Services;
using Snap.Application.Common.BackgroundJobs;
using Snap.Application.Common.Interfaces.BackgroundJobs;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Drivers.Services;
using Snap.Application.Explore.Interfaces;
using Snap.Application.Explore.Services;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Orders.Services;
using Snap.Application.Orders.Settings;
using Snap.Application.Points.Interfaces;
using Snap.Application.Points.Services;
using Snap.Application.Points.Settings;
using Snap.Application.SavedAddresses.Interfaces;
using Snap.Application.SavedAddresses.Services;
using Snap.Application.TripsHistory.Interfaces;
using Snap.Application.TripsHistory.Services;
using Snap.Application.UserHistory.Interfaces;
using Snap.Application.UserHistory.Services;
using Snap.Application.Users.Interfaces;
using Snap.Application.Users.Services;

namespace Snap.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSnapApplication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<OrderSettings>(configuration.GetSection(OrderSettings.SectionName));
            services.Configure<PointsSettings>(configuration.GetSection(PointsSettings.SectionName));

            // ── Location / driver state (singleton — shared across all requests) ────
            services.AddSingleton<IDriverLocationService, DriverLocationService>();
            services.AddScoped<ILocationService, LocationService>();
            services.AddScoped<IDriverService, DriverService>();

            // ── Background job infrastructure (singleton — owns the channel) ───────
            services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();

            // ── Order business logic (scoped) ───────────────────────────────────────
            services.AddScoped<IOrderService, OrderService>();

            // Concrete OrderNotificationService is registered under its own type so the
            // v2 decorator below can compose over it; IOrderNotificationService itself
            // resolves to the decorator for every existing caller (OrderService,
            // ScheduledRideProcessorService) with zero changes to those files.
            services.AddScoped<OrderNotificationService>();
            services.AddScoped<IOrderNotificationService, OrderNotificationServiceV2Decorator>();

            // ── v2 business layer ────────────────────────────────────────────────────
            services.AddScoped<IOrderV2QueryService, OrderV2QueryService>();
            services.AddScoped<IOrderV2CommandService, OrderV2CommandService>();
            services.AddScoped<IExplorePlaceService, ExplorePlaceService>();
            services.AddScoped<IPointsService, PointsService>();
            services.AddScoped<ICancelReasonService, CancelReasonService>();

            // ── Previously-service-less V1 controllers (extracted per architecture rule) ──
            services.AddScoped<IAppVersionService, AppVersionService>();
            services.AddScoped<ICarDataService, CarDataService>();
            services.AddScoped<ISavedAddressService, SavedAddressService>();
            services.AddScoped<ITripsHistoryService, TripsHistoryService>();
            services.AddScoped<IUserHistoryService, UserHistoryService>();
            services.AddScoped<IFcmTokenService, FcmTokenService>();

            return services;
        }
    }
}
