using System.Diagnostics;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Snap.Infrastructure.Notifications.Firebase
{
    /// <summary>
    /// Keeps the FCM path warm so real notifications never pay the cold-start cost.
    ///
    /// The first FCM call after startup (and after the 1-hour OAuth token expires) has to
    /// fetch a Google access token and open a new HTTPS connection. On this server that took
    /// ~12 s, and every push sent meanwhile queued behind it and then arrived all at once.
    /// A dry-run send (validated by FCM, delivered to nobody) at startup and every few minutes
    /// does that work in the background instead of inside a user's request.
    /// </summary>
    public sealed class FcmWarmupService : BackgroundService
    {
        // Well inside the 1-hour token lifetime, and frequent enough to keep the HTTP connection pooled.
        private static readonly TimeSpan Interval      = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(2);

        private readonly ILogger<FcmWarmupService> _logger;

        public FcmWarmupService(ILogger<FcmWarmupService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await WarmUpAsync(stoppingToken);

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task WarmUpAsync(CancellationToken ct)
        {
            var app = FirebaseApp.DefaultInstance;
            if (app is null) return;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await FirebaseMessaging.GetMessaging(app).SendAsync(
                    new Message { Topic = "warmup" },
                    dryRun: true,
                    ct);

                if (stopwatch.Elapsed > SlowThreshold)
                    _logger.LogWarning("FCM warm-up slow: {Ms} ms", stopwatch.ElapsedMilliseconds);
                else
                    _logger.LogInformation("FCM warm-up in {Ms} ms", stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM warm-up failed after {Ms} ms", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
