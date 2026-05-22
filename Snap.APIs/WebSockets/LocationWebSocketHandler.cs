using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Snap.APIs.DTOs;
using Snap.APIs.Services;
using Snap.Repository.Data;

namespace Snap.APIs.WebSockets
{
    /// <summary>
    /// Handles all /ws/location connections.
    ///
    /// Single source of truth fix: all driver state is stored in
    /// IDriverLocationService (a singleton). The old WebSocketMiddleware kept
    /// a parallel _onlineDrivers dict that was never read by OrderService,
    /// meaning GetNearestDriverIds always returned zero drivers. This handler
    /// writes every connect / update / disconnect through the service.
    /// </summary>
    public sealed class LocationWebSocketHandler : ILocationWebSocketHandler
    {
        private static readonly JsonSerializerOptions ParseOptions =
            new() { PropertyNameCaseInsensitive = true };

        private readonly IWebSocketHub          _hub;
        private readonly IDriverLocationService _locationService;
        private readonly IServiceScopeFactory   _scopeFactory;
        private readonly ILogger<LocationWebSocketHandler> _logger;

        public LocationWebSocketHandler(
            IWebSocketHub                      hub,
            IDriverLocationService             locationService,
            IServiceScopeFactory               scopeFactory,
            ILogger<LocationWebSocketHandler>  logger)
        {
            _hub             = hub;
            _locationService = locationService;
            _scopeFactory    = scopeFactory;
            _logger          = logger;
        }

        public async Task HandleAsync(WebSocket socket, string connectionId, CancellationToken ct)
        {
            _hub.RegisterLocationSocket(connectionId, socket);
            var buffer = new byte[4 * 1024];

            try
            {
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", ct);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessageAsync(socket, connectionId, text, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                _logger.LogDebug("Location WS closed unexpectedly: {Msg}", ex.Message);
            }
            finally
            {
                await OnDisconnectedAsync(connectionId, ct);
            }
        }

        // ── Message dispatch ──────────────────────────────────────────────────────

        private async Task ProcessMessageAsync(
            WebSocket socket, string connectionId, string text, CancellationToken ct)
        {
            try
            {
                using var doc    = JsonDocument.Parse(text);
                var       action = doc.RootElement.GetProperty("action").GetString();

                switch (action)
                {
                    case "ConnectDriver":
                        var driverId = doc.RootElement.GetProperty("driverId").GetInt32();
                        await OnDriverConnectedAsync(socket, connectionId, driverId, ct);
                        break;

                    case "ConnectClient":
                        var all = _locationService.GetConnectedDrivers();
                        await _hub.SendToSocketAsync(socket, new { action = "OnlineDrivers", data = all }, ct);
                        break;

                    case "UpdateLocation":
                        await OnLocationUpdatedAsync(connectionId, doc, ct);
                        break;

                    case "GetOnlineDrivers":
                        var online = _locationService.GetConnectedDrivers();
                        await _hub.SendToSocketAsync(socket, new { action = "OnlineDrivers", data = online }, ct);
                        break;

                    case "GetDriverLocation":
                        var targetId = doc.RootElement.GetProperty("driverId").GetInt32();
                        var loc      = _locationService.GetDriverLocation(targetId);
                        if (loc != null)
                            await _hub.SendToSocketAsync(socket, new { action = "DriverLocation",  data = loc }, ct);
                        else
                            await _hub.SendToSocketAsync(socket, new { action = "DriverNotFound", data = new { driverId = targetId } }, ct);
                        break;

                    case "Ping":
                        await _hub.SendToSocketAsync(socket, new { action = "Pong", data = new { timestamp = DateTime.UtcNow } }, ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing location WS message");
                try
                {
                    await _hub.SendToSocketAsync(
                        socket, new { action = "Error", data = new { message = ex.Message } }, ct);
                }
                catch { }
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private async Task OnDriverConnectedAsync(
            WebSocket socket, string connectionId, int driverId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SnapDbContext>();

            var driver = await db.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Select(d => new { d.Id, d.DriverFullname })
                .FirstOrDefaultAsync(ct);

            if (driver == null) return;

            _hub.BindLocationSocketToDriver(connectionId, driverId);
            _locationService.UpdateLocation(driverId, driver.DriverFullname, lat: 0, lng: 0);

            var info = _locationService.GetDriverLocation(driverId)!;
            await _hub.BroadcastLocationEventAsync("DriverConnected", info, ct);
            await _hub.SendToSocketAsync(socket, new { action = "DriverConnected", data = info }, ct);
        }

        private async Task OnLocationUpdatedAsync(
            string connectionId, JsonDocument doc, CancellationToken ct)
        {
            if (!_hub.TryGetLocationDriver(connectionId, out var driverId)) return;

            var existing = _locationService.GetDriverLocation(driverId);
            if (existing == null) return;

            var locationEl = doc.RootElement.GetProperty("location");
            var location   = JsonSerializer.Deserialize<LocationUpdateDto>(
                                 locationEl.GetRawText(), ParseOptions);
            if (location == null) return;

            _locationService.UpdateLocation(driverId, existing.DriverName, location.Lat, location.Lng);

            var updated = _locationService.GetDriverLocation(driverId)!;
            await _hub.BroadcastLocationEventAsync("LocationUpdate", updated, ct);
        }

        private async Task OnDisconnectedAsync(string connectionId, CancellationToken ct)
        {
            _hub.UnregisterLocationSocket(connectionId, out var driverId);

            if (driverId.HasValue)
            {
                var snapshot = _locationService.GetDriverLocation(driverId.Value);
                _locationService.RemoveDriver(driverId.Value);

                if (snapshot != null)
                {
                    snapshot.IsOnline = false;
                    await _hub.BroadcastLocationEventAsync("DriverDisconnected", snapshot, ct);
                }
            }
        }
    }
}
