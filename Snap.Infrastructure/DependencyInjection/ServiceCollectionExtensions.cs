using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snap.Application.Common.Interfaces.Notifications;
using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Identity.Interfaces;
using Snap.Application.Orders.Interfaces;
using Snap.Application.Points.Interfaces;
using Snap.Application.SavedAddresses.Interfaces;
using Snap.Application.TripsHistory.Interfaces;
using Snap.Application.UserHistory.Interfaces;
using Snap.Infrastructure.BackgroundJobs;
using Snap.Infrastructure.Identity.Token;
using Snap.Infrastructure.Notifications.Firebase;
using Snap.Infrastructure.Persistence;
using Snap.Infrastructure.Realtime.WebSockets;
using Snap.Infrastructure.Repositories;

namespace Snap.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSnapInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // ── Persistence ──────────────────────────────────────────────────────────
            services.AddDbContext<SnapDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"), sqlServerOptionsAction: sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                })
            );

            // ── v2: Data Access Layer (repository pattern over the existing SnapDbContext) ──
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IOrderRepositoryV2, OrderRepositoryV2>();
            services.AddScoped<IUserPointsRepository, UserPointsRepository>();

            // ── Orders persistence seam (Application-defined interfaces) ───────────
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderWorkflowRepository, OrderWorkflowRepository>();
            services.AddScoped<IOrderNotificationRepository, OrderNotificationRepository>();

            // ── Feature repositories for previously-service-less V1 controllers ────
            services.AddScoped<IDriverRepository, DriverRepository>();
            services.AddScoped<ISavedAddressRepository, SavedAddressRepository>();
            services.AddScoped<ITripsHistoryRepository, TripsHistoryRepository>();
            services.AddScoped<IUserHistoryRepository, UserHistoryRepository>();

            // ── Notification (FCM) / Identity (JWT) implementations ─────────────────
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<ITokenService, TokenService>();

            // ── v2: SignalR real-time surface ───────────────────────────────────────
            services.AddSignalR();

            // ── Realtime — native WebSocket transport (singletons own the connection dicts) ──
            services.AddSingleton<IWebSocketHub, WebSocketHub>();
            services.AddSingleton<ILocationWebSocketHandler, LocationWebSocketHandler>();
            services.AddSingleton<IOrderWebSocketHandler, OrderWebSocketHandler>();
            services.AddScoped<IOrderWebSocketNotifier, OrderWebSocketNotifier>();
            services.AddScoped<ILocationRealtimeNotifier, LocationRealtimeNotifier>();

            // ── Background job infrastructure ───────────────────────────────────────
            services.AddHostedService<BackgroundJobProcessor>();
            services.AddHostedService<ScheduledRideProcessorService>();

            // ── Startup seeders ───────────────────────────────────────────────────
            services.AddHostedService<DriverAvailabilityInitializer>();

            // Add Order Cancellation Background Service
            // services.AddHostedService<OrderCancellationService>();

            // Add Pending Order Deletion Background Service (Outbox Pattern) - Removed to avoid conflict with Cancellation Service
            // services.AddHostedService<PendingOrderDeletionService>();

            return services;
        }
    }
}
