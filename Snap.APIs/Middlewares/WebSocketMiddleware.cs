using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Snap.APIs.DTOs;
using Snap.Repository.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Snap.APIs.Middlewares
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private static readonly ConcurrentDictionary<string, WebSocket> _connections = new();
        private static readonly ConcurrentDictionary<string, int> _connectionToDriverMap = new();
        private static readonly ConcurrentDictionary<int, DriverLocationResponseDto> _onlineDrivers = new();

        // Order WebSocket connections: connectionId -> WebSocket
        private static readonly ConcurrentDictionary<string, WebSocket> _orderConnections = new();
        // Map connectionId -> driverId (set when driver subscribes)
        private static readonly ConcurrentDictionary<string, int> _orderConnectionToDriver = new();
        // Reverse map driverId -> connectionId (for targeted delivery)
        private static readonly ConcurrentDictionary<int, string> _driverToOrderConnection = new();

        // Static instance to allow access from controllers
        private static WebSocketMiddleware? _instance;
        public static WebSocketMiddleware? Instance => _instance;

        public WebSocketMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _instance = this;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws/location")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var connectionId = Guid.NewGuid().ToString();
                    _connections[connectionId] = webSocket;

                    try
                    {
                        await HandleWebSocket(webSocket, connectionId);
                    }
                    catch (Exception)
                    {
                        await HandleDisconnection(connectionId);
                    }
                    finally
                    {
                        await HandleDisconnection(connectionId);
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Expected a WebSocket request");
                }
            }
            else if (context.Request.Path == "/ws/orders")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var connectionId = Guid.NewGuid().ToString();
                    _orderConnections[connectionId] = webSocket;

                    try
                    {
                        await HandleOrderWebSocket(webSocket, connectionId);
                    }
                    catch (Exception) { }
                    finally
                    {
                        await HandleOrderDisconnection(connectionId);
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Expected a WebSocket request");
                }
            }
            else
            {
                await _next(context);
            }
        }

        private async Task HandleWebSocket(WebSocket webSocket, string connectionId)
        {
            var buffer = new byte[1024 * 4];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessage(webSocket, connectionId, message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    break;
                }
            }
        }

        private async Task ProcessMessage(WebSocket webSocket, string connectionId, string message)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(message);
                var action = jsonDoc.RootElement.GetProperty("action").GetString();

                switch (action)
                {
                    case "ConnectDriver":
                        var driverId = jsonDoc.RootElement.GetProperty("driverId").GetInt32();
                        await HandleConnectDriver(webSocket, connectionId, driverId);
                        break;

                    case "ConnectClient":
                        await HandleConnectClient(webSocket, connectionId);
                        break;

                    case "UpdateLocation":
                        await HandleUpdateLocation(webSocket, connectionId, jsonDoc);
                        break;

                    case "GetOnlineDrivers":
                        await SendOnlineDrivers(webSocket);
                        break;

                    case "GetDriverLocation":
                        var driverIdToGet = jsonDoc.RootElement.GetProperty("driverId").GetInt32();
                        await SendDriverLocation(webSocket, driverIdToGet);
                        break;

                    case "Ping":
                        await SendPong(webSocket);
                        break;
                }
            }
            catch (Exception ex)
            {
                await SendError(webSocket, $"Error processing message: {ex.Message}");
            }
        }

        private async Task HandleConnectDriver(WebSocket webSocket, string connectionId, int driverId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SnapDbContext>();

            _connectionToDriverMap[connectionId] = driverId;

            var driver = await context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId);
            if (driver != null)
            {
                var driverLocation = new DriverLocationResponseDto
                {
                    DriverId = driverId,
                    DriverName = driver.DriverFullname,
                    Lat = 0,
                    Lng = 0,
                    LastUpdate = DateTime.UtcNow,
                    IsOnline = true
                };

                _onlineDrivers.AddOrUpdate(driverId, driverLocation, (key, oldValue) => driverLocation);

                // Notify all clients
                await BroadcastToAll("DriverConnected", driverLocation);

                await SendMessage(webSocket, new { action = "DriverConnected", data = driverLocation });
            }
        }

        private async Task HandleConnectClient(WebSocket webSocket, string connectionId)
        {
            var onlineDrivers = _onlineDrivers.Values.ToList();
            await SendMessage(webSocket, new { action = "OnlineDrivers", data = onlineDrivers });
        }

        private async Task HandleUpdateLocation(WebSocket webSocket, string connectionId, JsonDocument jsonDoc)
        {
            if (_connectionToDriverMap.TryGetValue(connectionId, out var driverId))
            {
                var locationElement = jsonDoc.RootElement.GetProperty("location");
                //var location = JsonSerializer.Deserialize<LocationUpdateDto>(locationElement.GetRawText());

                var location = JsonSerializer.Deserialize<LocationUpdateDto>(
                                        locationElement.GetRawText(),
                                        new JsonSerializerOptions
                                        {
                                            PropertyNameCaseInsensitive = true
                                        });

                if (location != null && _onlineDrivers.TryGetValue(driverId, out var driverLocation))
                {
                    driverLocation.Lat = location.Lat;
                    driverLocation.Lng = location.Lng;
                    driverLocation.LastUpdate = location.Timestamp;
                    driverLocation.IsOnline = true;

                    // Broadcast to all clients
                    await BroadcastToAll("LocationUpdate", driverLocation);
                }
            }
        }

        private async Task SendOnlineDrivers(WebSocket webSocket)
        {
            var onlineDrivers = _onlineDrivers.Values.Where(d => d.IsOnline).ToList();
            await SendMessage(webSocket, new { action = "OnlineDrivers", data = onlineDrivers });
        }

        private async Task SendDriverLocation(WebSocket webSocket, int driverId)
        {
            if (_onlineDrivers.TryGetValue(driverId, out var driverLocation))
            {
                await SendMessage(webSocket, new { action = "DriverLocation", data = driverLocation });
            }
            else
            {
                await SendMessage(webSocket, new { action = "DriverNotFound", data = new { driverId } });
            }
        }

        private async Task SendPong(WebSocket webSocket)
        {
            await SendMessage(webSocket, new { action = "Pong", data = new { timestamp = DateTime.UtcNow } });
        }

        private async Task SendError(WebSocket webSocket, string error)
        {
            await SendMessage(webSocket, new { action = "Error", data = new { message = error } });
        }

        private async Task SendMessage(WebSocket webSocket, object message)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(message, options);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task BroadcastToAll(string action, object data)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var message = JsonSerializer.Serialize(new { action, data }, options);
            var bytes = Encoding.UTF8.GetBytes(message);

            foreach (var connection in _connections.Values)
            {
                if (connection.State == WebSocketState.Open)
                {
                    try
                    {
                        await connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { }
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Order WebSocket handlers
        // ──────────────────────────────────────────────

        private async Task HandleOrderWebSocket(WebSocket webSocket, string connectionId)
        {
            var buffer = new byte[1024 * 4];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessOrderMessage(webSocket, connectionId, message);
                }
            }
        }

        private async Task ProcessOrderMessage(WebSocket webSocket, string connectionId, string message)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(message);
                var action = jsonDoc.RootElement.GetProperty("action").GetString();

                switch (action)
                {
                    case "Subscribe":
                        // Driver registers: { "action": "Subscribe", "driverId": 123 }
                        var driverId = jsonDoc.RootElement.GetProperty("driverId").GetInt32();
                        _orderConnectionToDriver[connectionId] = driverId;
                        _driverToOrderConnection[driverId] = connectionId;
                        await SendMessage(webSocket, new { action = "Subscribed", data = new { driverId } });
                        break;

                    case "Ping":
                        await SendMessage(webSocket, new { action = "Pong", data = new { timestamp = DateTime.UtcNow } });
                        break;
                }
            }
            catch (Exception ex)
            {
                await SendError(webSocket, $"Error: {ex.Message}");
            }
        }

        private async Task HandleOrderDisconnection(string connectionId)
        {
            if (_orderConnectionToDriver.TryRemove(connectionId, out var driverId))
            {
                _driverToOrderConnection.TryRemove(driverId, out _);
            }
            _orderConnections.TryRemove(connectionId, out _);
        }

        private async Task BroadcastToOrderConnections(string action, object data)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var message = JsonSerializer.Serialize(new { action, data }, options);
            var bytes = Encoding.UTF8.GetBytes(message);

            foreach (var connection in _orderConnections.Values)
            {
                if (connection.State == WebSocketState.Open)
                {
                    try { await connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
                    catch { }
                }
            }
        }

        private async Task BroadcastToTargetDrivers(string action, object data, List<int> driverIds)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var message = JsonSerializer.Serialize(new { action, data }, options);
            var bytes = Encoding.UTF8.GetBytes(message);

            foreach (var driverId in driverIds)
            {
                if (_driverToOrderConnection.TryGetValue(driverId, out var connId)
                    && _orderConnections.TryGetValue(connId, out var ws)
                    && ws.State == WebSocketState.Open)
                {
                    try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
                    catch { }
                }
            }
        }

        // Called from OrdersController when a new order is created (targeted to eligible drivers)
        public static async Task BroadcastNewOrderToDrivers(object orderDto, List<int> driverIds)
        {
            if (_instance == null) return;
            if (driverIds.Count > 0)
                await _instance.BroadcastToTargetDrivers("NewOrder", orderDto, driverIds);
            else
                await _instance.BroadcastToOrderConnections("NewOrder", orderDto);
        }

        // Called when an order status changes (Accepted, Arrived, Started, Complete)
        public static async Task BroadcastOrderStatusUpdate(object orderDto)
        {
            if (_instance != null)
                await _instance.BroadcastToOrderConnections("OrderUpdated", orderDto);
        }

        // Called when an order is cancelled
        public static async Task BroadcastOrderCancelled(int orderId)
        {
            if (_instance != null)
                await _instance.BroadcastToOrderConnections("OrderCancelled", new { orderId });
        }

        // ──────────────────────────────────────────────

        // Public static method to broadcast from controllers
        public static async Task BroadcastLocationUpdate(DriverLocationResponseDto driverLocation)
        {
            if (_instance != null)
            {
                await _instance.BroadcastToAll("LocationUpdate", driverLocation);
            }
        }

        // Public static method to notify driver removal
        public static async Task BroadcastDriverRemoved(int driverId)
        {
            if (_instance != null)
            {
                await _instance.BroadcastToAll("DriverRemoved", new { driverId });
            }
        }

        // Public static method to update driver location in cache
        public static void UpdateDriverLocation(DriverLocationResponseDto driverLocation)
        {
            _onlineDrivers.AddOrUpdate(driverLocation.DriverId, driverLocation, (key, oldValue) => driverLocation);
        }

        private async Task HandleDisconnection(string connectionId)
        {
            if (_connectionToDriverMap.TryRemove(connectionId, out var driverId))
            {
                if (_onlineDrivers.TryGetValue(driverId, out var driverLocation))
                {
                    driverLocation.IsOnline = false;
                    driverLocation.LastUpdate = DateTime.UtcNow;
                    await BroadcastToAll("DriverDisconnected", driverLocation);
                }
            }

            _connections.TryRemove(connectionId, out _);
        }
    }
}

