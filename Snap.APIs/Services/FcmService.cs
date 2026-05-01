using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Snap.Core.Entities;
using Snap.Repository.Data;

namespace Snap.APIs.Services
{
    public interface IFcmService
    {
        Task NotifyDriversOfNewOrderAsync(Order order);
        Task<bool> SendToTokenAsync(string token, string title, string body, Dictionary<string, string>? data = null);
    }

    public class FcmService : IFcmService
    {
        private readonly SnapDbContext _context;
        private readonly ILogger<FcmService> _logger;
        private static bool _firebaseInitialized = false;
        private static readonly object _lock = new();

        public FcmService(SnapDbContext context, ILogger<FcmService> logger, IConfiguration config)
        {
            _context = context;
            _logger = logger;
            EnsureFirebaseInitialized(config);
        }

        private void EnsureFirebaseInitialized(IConfiguration config)
        {
            if (_firebaseInitialized) return;

            lock (_lock)
            {
                if (_firebaseInitialized) return;

                try
                {
                    var credentialsPath = config["Fcm:CredentialsPath"];
                    if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
                    {
                        _logger.LogWarning("Firebase credentials file not found at '{Path}'. FCM disabled.", credentialsPath);
                        return;
                    }

                    if (FirebaseApp.DefaultInstance == null)
                    {
                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = GoogleCredential.FromFile(credentialsPath)
                        });
                    }

                    _firebaseInitialized = true;
                    _logger.LogInformation("FirebaseApp initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize FirebaseApp.");
                }
            }
        }

        public async Task NotifyDriversOfNewOrderAsync(Order order)
        {
            if (!_firebaseInitialized)
            {
                _logger.LogWarning("FCM not initialized. Skipping notification for order {OrderId}.", order.Id);
                return;
            }

            var tokens = await _context.Drivers
                .Where(d => d.Status == "approved" && d.FcmToken != null && d.FcmToken != "")
                .Select(d => d.FcmToken!)
                .ToListAsync();

            if (tokens.Count == 0)
            {
                _logger.LogInformation("No approved drivers with FCM tokens. Skipping notification for order {OrderId}.", order.Id);
                return;
            }

            var data = new Dictionary<string, string>
            {
                ["type"] = "new_order",
                ["orderId"] = order.Id.ToString(),
                ["userId"] = order.UserId,
                ["from"] = order.From ?? "",
                ["to"] = order.To ?? "",
                ["fromLat"] = order.FromLatLng.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["fromLng"] = order.FromLatLng.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["toLat"] = order.ToLatLng.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["toLng"] = order.ToLatLng.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["expectedPrice"] = order.ExpectedPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["distance"] = order.Distance.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["carType"] = order.CarType ?? "",
                ["paymentWay"] = order.PaymentWay ?? "",
                ["noPassengers"] = order.NoPassengers.ToString(),
                ["pinkMode"] = order.PinkMode.ToString().ToLower()
            };

            var title = "اوردر جديد!";
            var body = $"من: {order.From} → إلى: {order.To} | {order.ExpectedPrice} جنيه";

            // FCM allows up to 500 tokens per multicast
            const int batchSize = 500;
            for (int i = 0; i < tokens.Count; i += batchSize)
            {
                var batch = tokens.Skip(i).Take(batchSize).ToList();
                var message = new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new Notification { Title = title, Body = body },
                    Data = data,
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            ChannelId = "new_orders_channel",
                            Sound = "default"
                        }
                    },
                    Apns = new ApnsConfig
                    {
                        Aps = new Aps { Sound = "default", ContentAvailable = true }
                    }
                };

                try
                {
                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                    _logger.LogInformation(
                        "Order {OrderId}: sent {Success}/{Total} FCM notifications.",
                        order.Id, response.SuccessCount, batch.Count);

                    // Cleanup invalid tokens
                    if (response.FailureCount > 0)
                    {
                        await CleanupInvalidTokensAsync(batch, response.Responses);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send FCM batch for order {OrderId}.", order.Id);
                }
            }
        }

        public async Task<bool> SendToTokenAsync(string token, string title, string body, Dictionary<string, string>? data = null)
        {
            if (!_firebaseInitialized) return false;

            try
            {
                var message = new Message
                {
                    Token = token,
                    Notification = new Notification { Title = title, Body = body },
                    Data = data
                };
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send FCM to token.");
                return false;
            }
        }

        private async Task CleanupInvalidTokensAsync(List<string> tokens, IReadOnlyList<SendResponse> responses)
        {
            var invalidTokens = new List<string>();
            for (int i = 0; i < responses.Count; i++)
            {
                if (!responses[i].IsSuccess)
                {
                    var code = responses[i].Exception?.MessagingErrorCode;
                    if (code == MessagingErrorCode.Unregistered || code == MessagingErrorCode.InvalidArgument)
                    {
                        invalidTokens.Add(tokens[i]);
                    }
                }
            }

            if (invalidTokens.Count == 0) return;

            var driversToClear = await _context.Drivers
                .Where(d => d.FcmToken != null && invalidTokens.Contains(d.FcmToken))
                .ToListAsync();

            foreach (var d in driversToClear)
            {
                d.FcmToken = null;
            }

            if (driversToClear.Count > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cleared {Count} invalid FCM tokens.", driversToClear.Count);
            }
        }
    }
}
