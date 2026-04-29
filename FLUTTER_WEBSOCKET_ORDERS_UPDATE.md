# Flutter Developer Update — Real-Time Orders via WebSocket

## What Changed

The backend now pushes new orders and order status changes **directly to the driver app** over WebSocket.  
Drivers no longer need to poll `GET /api/Orders` — the server sends events instantly.

---

## New WebSocket Endpoint

```
wss://gogos.site/ws/orders
```

This is separate from the existing location socket (`wss://gogos.site/ws/location`).

> **Important — URL must have NO `#` and NO explicit port.**  
> Use exactly `wss://gogos.site/ws/orders` — not `wss://gogos.site:443/ws/orders` and never with a `#` fragment.  
> A `#` at the end of a WebSocket URL makes the upgrade fail immediately (HTTP 400).  
> Check your URL-building code and make sure the port variable is not `0` or appended when it is the default HTTPS port.

---

## Connection & Protocol

All messages are JSON objects with an `action` field.

### 1. Connect and Subscribe (driver must do this on login / app start)

Send once after the socket opens:

```json
{ "action": "Subscribe", "driverId": 123 }
```

Server replies:

```json
{ "action": "Subscribed", "data": { "driverId": 123 } }
```

> **Why?** The server maps your `driverId` to your connection so it can push orders only to eligible nearby drivers (distance ≤ 10 km, matching PinkMode / CarType).

---

### 2. Keep-alive Ping

Send periodically (every 30 s) to prevent the connection from dropping:

```json
{ "action": "Ping" }
```

Server replies:

```json
{ "action": "Pong", "data": { "timestamp": "2026-04-20T10:00:00Z" } }
```

---

## Events You Will Receive

### `NewOrder` — a new trip request is available

Received when a user creates an order and you are a nearby eligible driver.

```json
{
  "action": "NewOrder",
  "data": {
    "id": 45,
    "userId": "abc-123",
    "date": "2026-04-20T10:05:00Z",
    "from": "Cairo Airport",
    "to": "Tahrir Square",
    "fromLatLng": { "lat": 30.1219, "lng": 31.4056 },
    "toLatLng":   { "lat": 30.0444, "lng": 31.2357 },
    "expectedPrice": 120.0,
    "type": "car",
    "distance": 35.2,
    "notes": null,
    "noPassengers": 2,
    "userImage": "https://...",
    "userName": "Ahmed Ali",
    "userPhone": "+201001234567",
    "status": "pending",
    "driverid": null,
    "review": 0,
    "paymentWay": "cash",
    "carType": "sedan",
    "pinkMode": false,
    "fcmToken": null
  }
}
```

**Action:** Show a popup / notification card to the driver with Accept / Ignore options.

---

### `OrderUpdated` — an order status changed

Received when any driver accepts an order, or when the trip progresses (Arrived → Started → Complete).

```json
{
  "action": "OrderUpdated",
  "data": {
    "orderId": 45,
    "status": "approved",   // approved | arrived | started | complete
    "driverid": 7
  }
}
```

**Action (driver app):**  
- If `status` is `"approved"` and `driverid` is **not your driver ID** → remove this order from your available orders list (someone else took it).  
- If `driverid` **is your driver ID** → update your active trip UI accordingly.

---

### `OrderCancelled` — an order was cancelled

Received when the user or driver cancels an order.

```json
{
  "action": "OrderCancelled",
  "data": { "orderId": 45 }
}
```

**Action:** Remove this order from your available orders list / active trip screen.

---

## Flutter Implementation Example

```dart
import 'dart:convert';
import 'package:web_socket_channel/web_socket_channel.dart';

class OrderWebSocketService {
  late WebSocketChannel _channel;
  final int driverId;

  OrderWebSocketService(this.driverId);

  void connect() {
    _channel = WebSocketChannel.connect(
      Uri.parse('ws://<your-host>/ws/orders'),
    );

    // Subscribe after connecting
    _channel.sink.add(jsonEncode({
      'action': 'Subscribe',
      'driverId': driverId,
    }));

    _channel.stream.listen(_onMessage, onDone: _onDone, onError: _onError);

    // Ping every 30 seconds
    Timer.periodic(const Duration(seconds: 30), (_) {
      _channel.sink.add(jsonEncode({'action': 'Ping'}));
    });
  }

  void _onMessage(dynamic raw) {
    final msg = jsonDecode(raw as String) as Map<String, dynamic>;
    switch (msg['action']) {
      case 'NewOrder':
        final order = msg['data'];
        // TODO: show new order card to driver
        break;
      case 'OrderUpdated':
        final data = msg['data'];
        // TODO: if data['driverid'] != myDriverId → remove from list
        break;
      case 'OrderCancelled':
        final orderId = msg['data']['orderId'];
        // TODO: remove order from UI
        break;
    }
  }

  void _onDone() {
    // Reconnect after a short delay
    Future.delayed(const Duration(seconds: 5), connect);
  }

  void _onError(error) {
    Future.delayed(const Duration(seconds: 5), connect);
  }

  void dispose() => _channel.sink.close();
}
```

---

## What You Can Remove / Stop Doing

| Old approach | Replacement |
|---|---|
| `GET /api/Orders` polling every N seconds | Listen for `NewOrder` events on `/ws/orders` |
| Manual refresh button to get new orders | Handled automatically by the socket |

> The REST endpoint `GET /api/Orders` still exists and can be called once on initial load to populate the list, but should **not** be polled afterwards.

---

## Dependencies (Flutter)

Add to `pubspec.yaml`:

```yaml
dependencies:
  web_socket_channel: ^2.4.0
```

---

## Summary of All WebSocket Endpoints

| Endpoint | Purpose |
|---|---|
| `ws://<host>/ws/location` | Driver location sharing (existing) |
| `ws://<host>/ws/orders` | Real-time order events (new) |
