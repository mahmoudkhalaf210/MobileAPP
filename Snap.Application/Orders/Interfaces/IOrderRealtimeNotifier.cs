namespace Snap.Application.Orders.Interfaces
{
    // Application-side abstraction over the v2 SignalR push. The Application layer knows
    // nothing about Hub/HubContext/connection groups — it just asks "tell this user" or
    // "tell everyone" that something changed, and the transport-specific implementation
    // (SignalR today) lives at the Snap.API composition boundary.
    public interface IOrderRealtimeNotifier
    {
        Task NotifyUserAsync(string userId, string eventName);
        Task NotifyAllAsync();
    }
}
