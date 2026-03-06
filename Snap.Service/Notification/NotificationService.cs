using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Snap.Core.Services;
using System.Threading.Tasks;

namespace Snap.Service.Notification
{
    public class NotificationService : INotificationService
    {
        public async Task SendNotification(string token, string title, string body)
        {
            if (string.IsNullOrEmpty(token)) return;

            // Ensure we use the default app instance
            var app = FirebaseApp.DefaultInstance;
            if (app == null)
            {
                throw new System.Exception("FirebaseApp.DefaultInstance is null. Firebase was not initialized in Program.cs.");
            }

            // Debug: Check if credential is present
            var options = app.Options;
            if (options.Credential == null)
            {
                 throw new System.Exception("FirebaseApp initialized but Credential is NULL.");
            }

            var messaging = FirebaseMessaging.GetMessaging(app);

            var message = new Message()
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = title,
                    Body = body
                }
            };

            await messaging.SendAsync(message);
        }
    }
}
