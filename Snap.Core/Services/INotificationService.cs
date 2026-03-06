using System.Threading.Tasks;

namespace Snap.Core.Services
{
    public interface INotificationService
    {
        Task SendNotification(string token, string title, string body);
    }
}
