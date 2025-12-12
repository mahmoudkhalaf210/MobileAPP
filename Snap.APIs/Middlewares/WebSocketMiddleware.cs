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
                    // Accept WebSocket connection
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var connectionId = Guid.NewGuid().ToString();
                    _connections[connectionId] = webSocket;

                    try
                    {
                        await HandleWebSocket(webSocket, connectionId);
                    }
                    catch (Exception ex)
                    {
                        // Log error if needed
                        await HandleDisconnection(connectionId);
                    }
                    finally
                    {
                        await HandleDisconnection(connectionId);
                    }
                }
                else
                {
                    // Not a WebSocket request - return 400
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
                var location = JsonSerializer.Deserialize<LocationUpdateDto>(locationElement.GetRawText());

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

