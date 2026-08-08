using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Snap.API.Composition;
using Snap.API.Errors;
using Snap.API.Extensions.Authentication;
using Snap.API.Hubs;
using Snap.API.Middlewares;
using Snap.Application.DependencyInjection;
using Snap.Application.Domain.Entities;
using Snap.Application.Orders.Interfaces;
using Snap.Infrastructure.DependencyInjection;
using Snap.Infrastructure.Persistence;
using Snap.Infrastructure.Persistence.Seeders;
using System.Text.Json.Serialization;

namespace Snap.API
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

            // ── Application + Infrastructure composition ────────────────────────────
            builder.Services.AddSnapApplication(builder.Configuration);
            builder.Services.AddSnapInfrastructure(builder.Configuration);

            // Configure Identity Services
            builder.Services.AddIdentityServices();

            // ── v2: SignalR realtime adapter (composition-boundary — needs the Hub type,
            // which must live in this project; Application only ever sees IOrderRealtimeNotifier) ──
            builder.Services.AddScoped<IOrderRealtimeNotifier, SignalROrderRealtimeNotifier>();

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
            app.MapHub<OrdersHubV2>("/hubs/v2/orders").RequireCors("SignalRPolicy");

            app.Run();
        }
    }
}
