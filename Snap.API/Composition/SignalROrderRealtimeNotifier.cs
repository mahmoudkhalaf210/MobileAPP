using Microsoft.AspNetCore.SignalR;
using Snap.API.Hubs;
using Snap.Application.Orders.Interfaces;

namespace Snap.API.Composition
{
    /// <summary>
    /// Composition-boundary implementation of the Application-owned IOrderRealtimeNotifier
    /// abstraction. Lives in Snap.API (not Snap.Application or Snap.Infrastructure) purely
    /// because IHubContext&lt;OrdersHubV2&gt; requires the OrdersHubV2 Hub type, and SignalR Hub
    /// types must live in the presentation project. Snap.Application knows nothing about
    /// Hub/HubContext/SignalR — it only calls NotifyUserAsync/NotifyAllAsync.
    /// </summary>
    public sealed class SignalROrderRealtimeNotifier : IOrderRealtimeNotifier
    {
        private readonly IHubContext<OrdersHubV2> _hub;

        public SignalROrderRealtimeNotifier(IHubContext<OrdersHubV2> hub)
        {
            _hub = hub;
        }

        public Task NotifyUserAsync(string userId, string eventName) =>
            _hub.Clients.Group(OrdersHubV2.UserGroup(userId)).SendAsync(eventName);

        public Task NotifyAllAsync() =>
            _hub.Clients.Group(OrdersHubV2.AllOrdersGroup).SendAsync("AllOrdersUpdated");
    }
}
