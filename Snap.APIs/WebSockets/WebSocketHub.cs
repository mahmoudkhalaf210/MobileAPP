using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Snap.APIs.WebSockets
{
    /// <summary>
    /// Singleton hub that owns all WebSocket connection state and broadcasting.
    ///
    /// Key design decisions:
    ///   • Message bytes are serialized ONCE before iterating connections —
    ///     avoids N serializations for N recipients.
    ///   • JsonSerializerOptions is a static readonly field — allocation happens
    ///     once at startup, not on every send.
    ///   • SendBytesAsync catches silently: a dropped connection must never
    ///     abort a broadcast to the remaining connections.
    ///   • ConcurrentDictionary gives lock-free reads on the hot path.
    /// </summary>
    public sealed class WebSocketHub : IWebSocketHub
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ── Location channel (/ws/location) ──────────────────────────────────────
        private readonly ConcurrentDictionary<string, WebSocket> _locationSockets  = new();
        private readonly ConcurrentDictionary<string, int>       _locationToDriver = new();

        // ── Order channel (/ws/orders) ────────────────────────────────────────────
        private readonly ConcurrentDictionary<string, WebSocket> _orderSockets      = new();
        private readonly ConcurrentDictionary<string, int>       _orderConnToDriver = new();
        private readonly ConcurrentDictionary<int, string>       _driverToOrderConn = new();

        // ── Location channel ──────────────────────────────────────────────────────

        public void RegisterLocationSocket(string connectionId, WebSocket socket)
            => _locationSockets[connectionId] = socket;

        public void UnregisterLocationSocket(string connectionId, out int? driverId)
        {
            _locationSockets.TryRemove(connectionId, out _);
            driverId = _locationToDriver.TryRemove(connectionId, out var id) ? id : null;
        }

        public void BindLocationSocketToDriver(string connectionId, int driverId)
            => _locationToDriver[connectionId] = driverId;

        public bool TryGetLocationDriver(string connectionId, out int driverId)
            => _locationToDriver.TryGetValue(connectionId, out driverId);

        // ── Order channel ─────────────────────────────────────────────────────────

        public void RegisterOrderSocket(string connectionId, WebSocket socket)
            => _orderSockets[connectionId] = socket;

        public void UnregisterOrderSocket(string connectionId)
        {
            _orderSockets.TryRemove(connectionId, out _);
            if (_orderConnToDriver.TryRemove(connectionId, out var driverId))
                _driverToOrderConn.TryRemove(driverId, out _);
        }

        public void BindOrderSocketToDriver(string connectionId, int driverId)
        {
            _orderConnToDriver[connectionId] = driverId;
            _driverToOrderConn[driverId]     = connectionId;
        }

        // ── Sends / broadcasts ────────────────────────────────────────────────────

        public Task SendToSocketAsync(WebSocket socket, object message, CancellationToken ct = default)
            => SendBytesAsync(socket, Serialize(message), ct);

        public Task BroadcastLocationEventAsync(string action, object data, CancellationToken ct = default)
            => BroadcastAsync(_locationSockets.Values, Serialize(new { action, data }), ct);

        public Task BroadcastNewOrderAsync(object order, IReadOnlyList<int>? targetDriverIds, CancellationToken ct = default)
        {
            var bytes = Serialize(new { action = "NewOrder", data = order });

            if (targetDriverIds is null)
                return BroadcastAsync(_orderSockets.Values, bytes, ct);

            var targets = new List<WebSocket>(targetDriverIds.Count);
            foreach (var id in targetDriverIds)
            {
                if (_driverToOrderConn.TryGetValue(id, out var connId) &&
                    _orderSockets.TryGetValue(connId, out var ws))
                    targets.Add(ws);
            }
            return BroadcastAsync(targets, bytes, ct);
        }

        public Task BroadcastOrderStatusAsync(object data, CancellationToken ct = default)
            => BroadcastAsync(_orderSockets.Values, Serialize(new { action = "OrderUpdated", data }), ct);

        public Task BroadcastOrderCancelledAsync(int orderId, CancellationToken ct = default)
            => BroadcastAsync(_orderSockets.Values, Serialize(new { action = "OrderCancelled", data = new { orderId } }), ct);

        // ── Private helpers ───────────────────────────────────────────────────────

        private static Task BroadcastAsync(IEnumerable<WebSocket> sockets, byte[] bytes, CancellationToken ct)
            => Task.WhenAll(sockets
                .Where(ws => ws.State == WebSocketState.Open)
                .Select(ws => SendBytesAsync(ws, bytes, ct)));

        private static async Task SendBytesAsync(WebSocket socket, byte[] bytes, CancellationToken ct)
        {
            try
            {
                await socket.SendAsync(
                    new ReadOnlyMemory<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    ct).ConfigureAwait(false);
            }
            catch { }
        }

        private static byte[] Serialize(object obj)
            => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, JsonOptions));
    }
}
