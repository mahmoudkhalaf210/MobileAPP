using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snap.APIs.Errors;
using Snap.APIs.Extensions;
using Snap.APIs.Middlewares;
using Snap.Core.Entities;
using Snap.Core.Services;
using Snap.Repository.Data;
using Snap.Repository.Seeders;
using Snap.Service.Notification;
using System.Text.Json.Serialization;

namespace Snap.APIs
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
            });

            // Configure DbContext
            builder.Services.AddDbContext<SnapDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlServerOptionsAction: sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5, 
                        maxRetryDelay: TimeSpan.FromSeconds(30), 
                        errorNumbersToAdd: null); 
                })
            );

            // Configure Identity Services
            builder.Services.AddIdentityServices();

            // ── Location / driver state (singleton — shared across all requests) ────
            builder.Services.AddSingleton<Snap.APIs.Services.IDriverLocationService,
                                          Snap.APIs.Services.DriverLocationService>();

            // ── WebSocket hub + handlers (singletons — own the connection dicts) ──
            builder.Services.AddSingleton<Snap.APIs.WebSockets.IWebSocketHub,
                                          Snap.APIs.WebSockets.WebSocketHub>();
            builder.Services.AddSingleton<Snap.APIs.WebSockets.ILocationWebSocketHandler,
                                          Snap.APIs.WebSockets.LocationWebSocketHandler>();
            builder.Services.AddSingleton<Snap.APIs.WebSockets.IOrderWebSocketHandler,
                                          Snap.APIs.WebSockets.OrderWebSocketHandler>();

            // ── Notification (scoped — uses DbContext + FCM per request/job) ───────
            builder.Services.AddScoped<INotificationService, NotificationService>();

            // Concrete OrderNotificationService is registered under its own type so the
            // v2 decorator below can compose over it; IOrderNotificationService itself
            // now resolves to the decorator for every existing caller (OrderService,
            // ScheduledRideProcessorService) with zero changes to those files.
            builder.Services.AddScoped<Snap.APIs.Services.OrderNotificationService>();
            builder.Services.AddScoped<Snap.APIs.Services.IOrderNotificationService,
                                       Snap.APIs.Services.V2.OrderNotificationServiceV2Decorator>();

            // ── Order business logic (scoped) ─────────────────────────────────────
            builder.Services.Configure<Snap.APIs.Settings.OrderSettings>(
                builder.Configuration.GetSection(Snap.APIs.Settings.OrderSettings.SectionName));
            builder.Services.AddScoped<Snap.APIs.Services.IOrderService,
                                       Snap.APIs.Services.OrderService>();

            // ── v2: SignalR real-time surface ───────────────────────────────────────
            builder.Services.AddSignalR();

            // ── v2: Data Access Layer (repository pattern over the existing SnapDbContext) ──
            builder.Services.AddScoped(typeof(Snap.DataAccess.Interfaces.IRepository<>),
                                       typeof(Snap.DataAccess.Repositories.Repository<>));
            builder.Services.AddScoped<Snap.DataAccess.Interfaces.IUnitOfWork,
                                       Snap.DataAccess.UnitOfWork.UnitOfWork>();
            builder.Services.AddScoped<Snap.DataAccess.Interfaces.IOrderRepositoryV2,
                                       Snap.DataAccess.Repositories.OrderRepositoryV2>();
            builder.Services.AddScoped<Snap.DataAccess.Interfaces.IUserPointsRepository,
                                       Snap.DataAccess.Repositories.UserPointsRepository>();

            // ── v2: Business Layer ──────────────────────────────────────────────────
            builder.Services.Configure<Snap.Business.Settings.PointsSettings>(
                builder.Configuration.GetSection(Snap.Business.Settings.PointsSettings.SectionName));
            builder.Services.AddScoped<Snap.Business.Interfaces.IOrderV2QueryService,
                                       Snap.Business.Services.OrderV2QueryService>();
            builder.Services.AddScoped<Snap.Business.Interfaces.IExplorePlaceService,
                                       Snap.Business.Services.ExplorePlaceService>();
            builder.Services.AddScoped<Snap.Business.Interfaces.IPointsService,
                                       Snap.Business.Services.PointsService>();

            // Composition adapter (lives in Snap.APIs — see OrderV2CommandService for why).
            builder.Services.AddScoped<Snap.Business.Interfaces.IOrderV2CommandService,
                                       Snap.APIs.Services.V2.OrderV2CommandService>();

            // ── Background job infrastructure (singletons) ────────────────────────
            builder.Services.AddSingleton<Snap.APIs.Services.IBackgroundJobQueue,
                                          Snap.APIs.Services.BackgroundJobQueue>();
            builder.Services.AddHostedService<Snap.APIs.Services.BackgroundJobProcessor>();
            builder.Services.AddHostedService<Snap.APIs.Services.ScheduledRideProcessorService>();

            // ── Startup seeders ───────────────────────────────────────────────────
            builder.Services.AddHostedService<Snap.APIs.Services.DriverAvailabilityInitializer>();

            // Add Order Cancellation Background Service
           // builder.Services.AddHostedService<Snap.APIs.Services.OrderCancellationService>();
            
            // Add Pending Order Deletion Background Service (Outbox Pattern) - Removed to avoid conflict with Cancellation Service
            // builder.Services.AddHostedService<Snap.APIs.Services.PendingOrderDeletionService>();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.Configure<ApiBehaviorOptions>(
             Options =>{Options
                .InvalidModelStateResponseFactory = (actionContext) =>
            {
                var errors = actionContext.ModelState
                .Where(p => p.Value.Errors.Count() > 0)
                .SelectMany(p => p.Value.Errors)
                .Select(E => E.ErrorMessage)
                .ToArray();

            var ValidationErrorResponse = new ApiValidationErrorResponse() 
             { Erorrs = errors};
                //return new BadRequestObjectResult(ValidationErrorResponse)
                // Return the format you want
                return new BadRequestObjectResult(new
                {
                    statusCode = 400,
                    message = string.Join(" ", errors),
                    errors = errors
                });
                ;
            };});


            // Trust the reverse-proxy (nginx) for forwarded headers so the app
            // knows the real scheme (https) and client IP behind the proxy.
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
                    
                options.AddPolicy("SignalRPolicy",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyHeader()
                              .AllowAnyMethod()
                              .SetIsOriginAllowed(_ => true);
                    });
            });


            // Initialize Firebase (ONLY ONCE)
            // Use BaseDirectory to ensure we find the file in the output directory
            var credentialPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "firebase-service-account.json");

            // Verify file exists to avoid confusing "missing credential" errors later
            if (!File.Exists(credentialPath))
            {
                // Fallback to ContentRootPath if not in bin
                credentialPath = Path.Combine(
                    builder.Environment.ContentRootPath, 
                    "firebase-service-account.json");
                
                if (!File.Exists(credentialPath))
                {
                    throw new FileNotFoundException($"Firebase credential file not found at: {credentialPath}. Make sure it is copied to output directory.");
                }
            }

            // Set environment variable for Google Client libraries
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);

            if (FirebaseApp.DefaultInstance == null)
            {
                Console.WriteLine($"[Program] Initializing Firebase with credential: {credentialPath}");
                try 
                {
                    // Explicitly create credential with scopes
                    var credential = GoogleCredential.FromFile(credentialPath)
                        .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                    
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = credential,
                        ProjectId = "gogo-8f4a5" // Explicitly set ProjectId from JSON
                    });
                    Console.WriteLine("[Program] Firebase initialized successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Program] CRITICAL ERROR initializing Firebase: {ex.Message}");
                    throw;
                }
            }
            else
            {
                 Console.WriteLine("[Program] Firebase already initialized.");
            }

            var app = builder.Build();

            #region Apply Migrations and Seed Data

            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();

            try
            {
                var dbContext = services.GetRequiredService<SnapDbContext>();

                // Apply pending migrations
                // NOTE: Commented out because the database is out of sync with migrations (tables exist but history is missing).
                // To fix, we manually created missing tables and seeded data.
                // await dbContext.Database.MigrateAsync();

                // Seed default users
                var userManager = services.GetRequiredService<UserManager<User>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                var logger = loggerFactory.CreateLogger<Program>();

                logger.LogInformation("Seeding default users...");
                await UserSeed.SeedUserAsync(userManager, roleManager);
                logger.LogInformation("User seeding completed.");
            }
            catch (Exception e)
            {
                var logger = loggerFactory.CreateLogger<Program>();
                logger.LogError(e, "An error occurred while applying migrations and seeding users.");
            }

            #endregion

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
                app.UseMiddleware<ExceptionMiddleware>();
                app.UseDeveloperExceptionPage();    
                app.UseSwagger();
                app.UseSwaggerUI();
            //}

            // Must be first: decode X-Forwarded-For / X-Forwarded-Proto from nginx
            app.UseForwardedHeaders();

            app.UseCors("AllowAll");

            // Enable WebSocket support before any middleware that might redirect/close the connection.
            // UseHttpsRedirection is intentionally omitted — nginx handles SSL termination and
            // already enforces HTTPS; running it here would break WebSocket upgrade requests that
            // arrive from nginx over plain HTTP on the internal network.
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            app.UseAuthentication();
            app.UseAuthorization();

            // WebSocket Middleware must be after UseWebSockets and before MapControllers
            app.UseMiddleware<WebSocketMiddleware>();

            app.MapControllers();

            // v2 real-time surface — additive, does not replace the WebSocket layer above.
            app.MapHub<Snap.APIs.Hubs.OrdersHubV2>("/hubs/v2/orders").RequireCors("SignalRPolicy");

            app.Run();
        }
    }
}
