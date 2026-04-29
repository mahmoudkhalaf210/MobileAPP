using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Snap.Core.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Snap.Service.Notification
{
    public class NotificationService : INotificationService
    {
        public async Task SendNotification(string token, string title, string body, IDictionary<string, string> data = null)
        {
            if (string.IsNullOrEmpty(token)) return;

            // Ensure we use the default app instance
            var app = FirebaseApp.DefaultInstance;
            if (app == null)
            {
                throw new System.Exception("FirebaseApp.DefaultInstance is null. Firebase was not initialized in Program.cs.");
            }

            var messaging = FirebaseMessaging.GetMessaging(app);

            var message = new Message()
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = title,
                    Body = body
                },
                Data = data != null ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(data) : null
            };

            await messaging.SendAsync(message);
        }
    }
}
