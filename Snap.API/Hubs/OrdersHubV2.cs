using Microsoft.AspNetCore.SignalR;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;

namespace Snap.API.Hubs
{
    /// <summary>
    /// v2 real-time order surface. Additive — the existing native WebSocket layer
    /// (Snap.Infrastructure/Realtime/WebSockets/*) and Snap.API.Hubs.LocationHub are untouched.
    ///
    /// Group model: clients call <see cref="Register"/> with their userId to join a
    /// per-user group; all connections implicitly join the global "all-orders" feed
    /// on connect. There is no [Authorize] anywhere in this app today, so userId is
    /// supplied by the client — the same trust model already used by every REST
    /// endpoint (not a regression introduced here).
    /// </summary>
    public class OrdersHubV2 : Hub
    {
        public const string AllOrdersGroup = "all-orders";
        public static string UserGroup(string userId) => $"user-{userId}";

        private readonly IOrderV2QueryService _queryService;

        public OrdersHubV2(IOrderV2QueryService queryService)
        {
            _queryService = queryService;
        }

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AllOrdersGroup);
            await base.OnConnectedAsync();
        }

        /// <summary>Joins the caller's connection to their personal order-updates group.</summary>
        public Task Register(string userId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        public Task<List<OrderV2Dto>> GetAllOrders() => _queryService.GetAllOrdersAsync();

        public Task<List<OrderV2Dto>> GetScheduledOrdersForUser(string userId) =>
            _queryService.GetScheduledForUserAsync(userId);

        public Task<List<OrderV2Dto>> GetActiveOrders(string userId) =>
            _queryService.GetActiveForUserAsync(userId);

        public Task<List<OrderV2Dto>> GetCompletedOrders(string userId) =>
            _queryService.GetCompletedForUserAsync(userId);

        public Task<List<OrderV2Dto>> GetCancelledOrders(string userId) =>
            _queryService.GetCancelledForUserAsync(userId);
    }
}
