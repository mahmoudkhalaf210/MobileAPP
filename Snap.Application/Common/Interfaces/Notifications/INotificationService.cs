using System.Collections.Generic;
using System.Threading.Tasks;

namespace Snap.Application.Common.Interfaces.Notifications
{
    public interface INotificationService
    {
        Task SendNotification(string token, string title, string body, IDictionary<string, string> data = null);

        /// <summary>
        /// Sends notifications in sequential batches of <paramref name="batchSize"/> to avoid
        /// overwhelming the FCM endpoint when targeting many devices simultaneously.
        /// Duplicate and empty tokens are deduplicated before sending.
        /// </summary>
        Task SendBatchNotificationsAsync(
            IEnumerable<string> tokens,
            string title,
            string body,
            IDictionary<string, string> data = null,
            int batchSize = 20);
    }
}
