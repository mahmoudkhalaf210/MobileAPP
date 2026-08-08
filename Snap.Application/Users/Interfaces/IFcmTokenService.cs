namespace Snap.Application.Users.Interfaces
{
    public interface IFcmTokenService
    {
        Task SaveTokenAsync(string userId, string token);
    }
}
