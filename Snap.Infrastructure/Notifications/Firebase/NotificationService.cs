using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Snap.Application.Common.Interfaces.Notifications;

namespace Snap.Infrastructure.Notifications.Firebase
{
    public class NotificationService : INotificationService
    {
        private const string AndroidChannelId = "high_importance_channel";
        private const int    MaxRetries       = 3;

        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
        }

        public async Task SendNotification(
            string token, string title, string body,
            IDictionary<string, string>? data = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            var messaging = GetMessaging();
            var message   = BuildMessage(token, title, body, data);

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await messaging.SendAsync(message);
                    return; // success
                }
                catch (FirebaseMessagingException ex) when (IsTransient(ex) && attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(200 * attempt);
                    _logger.LogWarning(
                        "FCM attempt {Attempt}/{Max} failed ({Code}). Retrying in {Delay} ms…",
                        attempt, MaxRetries, ex.MessagingErrorCode, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }
                catch (FirebaseMessagingException ex) when (!IsTransient(ex))
                {
                    // Permanent failure (invalid/unregistered token, wrong sender-id…).
                    // Retrying will always fail — log and bail out immediately.
                    _logger.LogWarning(
                        "FCM permanent failure ({Code}) — skipping token {Prefix}…",
                        ex.MessagingErrorCode, SafePrefix(token));
                    return;
                }
            }

            // Reached only when all retry attempts were transient failures
            _logger.LogError(
                "FCM send failed after {Max} attempts for token {Prefix}…",
                MaxRetries, SafePrefix(token));
        }

        /// <summary>
        /// Deduplicates tokens then sends in sequential batches of <paramref name="batchSize"/>.
        /// Each batch runs concurrently; individual failures are retried then logged without
        /// aborting the remaining notifications.
        /// </summary>
        public async Task SendBatchNotificationsAsync(
            IEnumerable<string> tokens,
            string title,
            string body,
            IDictionary<string, string>? data = null,
            int batchSize = 20)
        {
            var unique = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (unique.Count == 0) return;

            var messaging = GetMessaging();

            for (int i = 0; i < unique.Count; i += batchSize)
            {
                var batch = unique.Skip(i).Take(batchSize);

                await Task.WhenAll(batch.Select(token =>
                    SendWithRetryAsync(messaging, token, title, body, data)));
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private async Task SendWithRetryAsync(
            FirebaseMessaging messaging,
            string token,
            string title, string body,
            IDictionary<string, string>? data)
        {
            var message = BuildMessage(token, title, body, data);

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await messaging.SendAsync(message);
                    return;
                }
                catch (FirebaseMessagingException ex) when (IsTransient(ex) && attempt < MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
                }
                catch (FirebaseMessagingException ex) when (!IsTransient(ex))
                {
                    _logger.LogWarning("FCM permanent failure ({Code}) for token {Prefix}…",
                        ex.MessagingErrorCode, SafePrefix(token));
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("FCM unexpected error for token {Prefix}…: {Msg}",
                        SafePrefix(token), ex.Message);
                    return;
                }
            }

            _logger.LogError("FCM send failed after {Max} attempts for token {Prefix}…",
                MaxRetries, SafePrefix(token));
        }

        /// <summary>
        /// Transient errors are worth retrying (temporary outage on Firebase side).
        /// Permanent errors (bad token, wrong project, etc.) will always fail — don't retry.
        /// </summary>
        private static bool IsTransient(FirebaseMessagingException ex) =>
            ex.MessagingErrorCode is MessagingErrorCode.Unavailable
                                  or MessagingErrorCode.Internal
                                  or MessagingErrorCode.QuotaExceeded;

        private static FirebaseMessaging GetMessaging()
        {
            var app = FirebaseApp.DefaultInstance
                ?? throw new InvalidOperationException(
                    "FirebaseApp.DefaultInstance is null. Firebase was not initialised in Program.cs.");
            return FirebaseMessaging.GetMessaging(app);
        }

        private static Message BuildMessage(
            string token, string title, string body, IDictionary<string, string>? data) =>
            new()
            {
                Token        = token,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Android      = new AndroidConfig
                {
                    Notification = new AndroidNotification
                    {
                        ChannelId = AndroidChannelId,
                        Sound     = "default"
                    }
                },
                Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } },
                Data = data is { Count: > 0 }
                    ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                        new Dictionary<string, string>(data))
                    : null
            };

        // Show only first 8 chars so tokens are identifiable without being leaked in logs
        private static string SafePrefix(string token) =>
            token.Length > 8 ? token[..8] : token;
    }
}
