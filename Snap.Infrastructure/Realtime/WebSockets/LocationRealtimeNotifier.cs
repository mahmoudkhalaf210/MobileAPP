using Snap.Application.Drivers.Interfaces;

namespace Snap.Infrastructure.Realtime.WebSockets
{
    // Thin Infrastructure-side implementation of the Application-owned
    // ILocationRealtimeNotifier abstraction — forwards to the transport-level IWebSocketHub.
    public sealed class LocationRealtimeNotifier : ILocationRealtimeNotifier
    {
        private readonly IWebSocketHub _hub;

        public LocationRealtimeNotifier(IWebSocketHub hub)
        {
            _hub = hub;
        }

        public Task BroadcastLocationEventAsync(string action, object data, CancellationToken ct = default) =>
            _hub.BroadcastLocationEventAsync(action, data, ct);
    }
}
