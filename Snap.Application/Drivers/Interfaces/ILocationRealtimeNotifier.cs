namespace Snap.Application.Drivers.Interfaces
{
    // Application-side abstraction over the native /ws/location WebSocket broadcast.
    // Mirrors IWebSocketHub.BroadcastLocationEventAsync (which stays entirely inside
    // Snap.Infrastructure, since it carries raw System.Net.WebSockets types).
    public interface ILocationRealtimeNotifier
    {
        Task BroadcastLocationEventAsync(string action, object data, CancellationToken ct = default);
    }
}
