# دليل التغيير من SignalR إلى WebSocket العادي
# Guide: Migrate from SignalR to Native WebSocket

## 📋 **الهدف - Objective**

استبدال SignalR بـ WebSocket عادي (WSC) لأن Flutter يريد WebSocket مباشر بدون SignalR.

---

## 🔧 **التغييرات المطلوبة - Required Changes**

### **1. إزالة SignalR Package**

#### **في `Snap.APIs/Snap.APIs.csproj`:**
```xml
<!-- احذف هذا السطر -->
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
```

---

### **2. إنشاء WebSocket Middleware**

#### **إنشاء ملف جديد: `Snap.APIs/Middlewares/WebSocketMiddleware.cs`**

```csharp
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
        private readonly SnapDbContext _context;
        private static readonly ConcurrentDictionary<string, WebSocket> _connections = new();
        private static readonly ConcurrentDictionary<string, int> _connectionToDriverMap = new();
        private static readonly ConcurrentDictionary<int, DriverLocationResponseDto> _onlineDrivers = new();

        public WebSocketMiddleware(RequestDelegate next, SnapDbContext context)
        {
            _next = next;
            _context = context;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws/location" && context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var connectionId = Guid.NewGuid().ToString();
                _connections[connectionId] = webSocket;

                try
                {
                    await HandleWebSocket(webSocket, connectionId);
                }
                finally
                {
                    await HandleDisconnection(connectionId);
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
            _connectionToDriverMap[connectionId] = driverId;

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId);
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
                var location = JsonSerializer.Deserialize<LocationUpdateDto>(jsonDoc.RootElement.GetProperty("location").GetRawText());

                if (_onlineDrivers.TryGetValue(driverId, out var driverLocation))
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
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task BroadcastToAll(string action, object data)
        {
            var message = JsonSerializer.Serialize(new { action, data });
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
```

---

### **3. تحديث Program.cs**

#### **في `Snap.APIs/Program.cs`:**

```csharp
// احذف هذه الأسطر:
using Snap.APIs.Hubs;  // احذف
builder.Services.AddSignalR();  // احذف
app.MapHub<LocationHub>("/locationHub");  // احذف

// أضف هذه:
using Snap.APIs.Middlewares;  // أضف
using Snap.Repository.Data;  // أضف

// في Configure Services:
builder.Services.AddDbContext<SnapDbContext>(options => ...);  // موجود بالفعل

// في Configure Pipeline (قبل app.MapControllers()):
app.UseWebSockets();  // أضف هذا
app.UseMiddleware<WebSocketMiddleware>();  // أضف هذا
```

**الكود الكامل للجزء المطلوب:**

```csharp
// في بداية الملف - أضف:
using Snap.APIs.Middlewares;

// احذف:
// using Snap.APIs.Hubs;
// builder.Services.AddSignalR();

// في Configure Pipeline:
app.UseWebSockets();  // أضف قبل app.UseCors
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// أضف WebSocket Middleware
app.UseMiddleware<WebSocketMiddleware>();

app.MapControllers();

// احذف:
// app.MapHub<LocationHub>("/locationHub");
```

---

### **4. تحديث LocationController**

#### **في `Snap.APIs/Controllers/LocationController.cs`:**

```csharp
// احذف:
using Microsoft.AspNetCore.SignalR;
using Snap.APIs.Hubs;
private readonly IHubContext<LocationHub> _hubContext;

// في Constructor - احذف:
public LocationController(IHubContext<LocationHub> hubContext, SnapDbContext context)
{
    _hubContext = hubContext;  // احذف
    _context = context;
}

// غير إلى:
public LocationController(SnapDbContext context)
{
    _context = context;
}

// في UpdateDriverLocation - احذف:
await _hubContext.Clients.Group("Clients").SendAsync("LocationUpdate", driverLocation);
await _hubContext.Clients.Group("Drivers").SendAsync("DriverLocationUpdate", driverLocation);

// (الـ WebSocket Middleware سيتولى البث تلقائياً)

// في RemoveDriverLocation - احذف:
await _hubContext.Clients.All.SendAsync("DriverRemoved", driverId);
```

---

### **5. حذف ملفات SignalR**

يمكنك حذف أو الاحتفاظ بهذه الملفات (لن تُستخدم):
- `Snap.APIs/Hubs/LocationHub.cs` - يمكن حذفه
- `Snap.APIs/WEBSOCKET_LOCATION_API.md` - يمكن حذفه أو تحديثه

---

## 📱 **استخدام WebSocket في Flutter**

### **Package المطلوب:**

```yaml
# في pubspec.yaml
dependencies:
  web_socket_channel: ^2.4.0
```

### **مثال الكود في Flutter:**

```dart
import 'package:web_socket_channel/web_socket_channel.dart';
import 'dart:convert';

class LocationWebSocketService {
  WebSocketChannel? _channel;
  
  // الاتصال
  void connect() {
    _channel = WebSocketChannel.connect(
      Uri.parse('ws://your-api.com/ws/location'),
    );
    
    // الاستماع للرسائل
    _channel!.stream.listen(
      (message) {
        final data = jsonDecode(message);
        handleMessage(data);
      },
      onError: (error) => print('WebSocket error: $error'),
      onDone: () => print('WebSocket closed'),
    );
  }
  
  // معالجة الرسائل
  void handleMessage(Map<String, dynamic> data) {
    final action = data['action'];
    
    switch (action) {
      case 'LocationUpdate':
        final location = data['data'];
        // تحديث الموقع
        break;
      case 'DriverConnected':
        // سائق جديد متصل
        break;
      case 'OnlineDrivers':
        final drivers = data['data'];
        // قائمة السائقين المتصلين
        break;
    }
  }
  
  // ربط السائق
  void connectDriver(int driverId) {
    sendMessage({
      'action': 'ConnectDriver',
      'driverId': driverId,
    });
  }
  
  // ربط العميل
  void connectClient() {
    sendMessage({
      'action': 'ConnectClient',
    });
  }
  
  // تحديث الموقع
  void updateLocation(double lat, double lng) {
    sendMessage({
      'action': 'UpdateLocation',
      'location': {
        'lat': lat,
        'lng': lng,
        'timestamp': DateTime.now().toIso8601String(),
      },
    });
  }
  
  // الحصول على السائقين المتصلين
  void getOnlineDrivers() {
    sendMessage({
      'action': 'GetOnlineDrivers',
    });
  }
  
  // إرسال رسالة
  void sendMessage(Map<String, dynamic> message) {
    if (_channel != null) {
      _channel!.sink.add(jsonEncode(message));
    }
  }
  
  // إغلاق الاتصال
  void disconnect() {
    _channel?.sink.close();
  }
}
```

---

## 🔄 **تنسيق الرسائل - Message Format**

### **من Flutter إلى Server:**

```json
{
  "action": "ConnectDriver",
  "driverId": 123
}

{
  "action": "UpdateLocation",
  "location": {
    "lat": 30.0444,
    "lng": 31.2357,
    "timestamp": "2024-12-12T10:00:00Z"
  }
}

{
  "action": "ConnectClient"
}

{
  "action": "GetOnlineDrivers"
}
```

### **من Server إلى Flutter:**

```json
{
  "action": "LocationUpdate",
  "data": {
    "driverId": 123,
    "driverName": "Khaled Ibrahim",
    "lat": 30.0444,
    "lng": 31.2357,
    "lastUpdate": "2024-12-12T10:00:00Z",
    "isOnline": true
  }
}

{
  "action": "OnlineDrivers",
  "data": [
    {
      "driverId": 123,
      "driverName": "Khaled Ibrahim",
      "lat": 30.0444,
      "lng": 31.2357,
      "lastUpdate": "2024-12-12T10:00:00Z",
      "isOnline": true
    }
  ]
}

{
  "action": "DriverConnected",
  "data": {
    "driverId": 123,
    "driverName": "Khaled Ibrahim",
    ...
  }
}
```

---

## ✅ **خطوات التنفيذ - Implementation Steps**

1. ✅ إنشاء `WebSocketMiddleware.cs`
2. ✅ تحديث `Program.cs` (إزالة SignalR، إضافة WebSocket)
3. ✅ تحديث `LocationController.cs` (إزالة SignalR references)
4. ✅ حذف `Microsoft.AspNetCore.SignalR` package
5. ✅ اختبار الاتصال من Flutter

---

## 🔗 **WebSocket Endpoint**

**URL:** `ws://localhost:5062/ws/location` (HTTP)  
**URL:** `wss://localhost:7155/ws/location` (HTTPS)

---

## 📝 **ملاحظات مهمة - Important Notes**

1. ✅ WebSocket يعمل على نفس الـ port مثل HTTP/HTTPS
2. ✅ استخدم `ws://` للـ HTTP و `wss://` للـ HTTPS
3. ✅ الـ Middleware يتعامل مع جميع الاتصالات تلقائياً
4. ✅ البيانات تُرسل كـ JSON strings
5. ✅ جميع الوظائف الموجودة في SignalR متوفرة في WebSocket

---

**تاريخ الإنشاء:** 2024-12-12  
**الحالة:** ✅ جاهز للتنفيذ

