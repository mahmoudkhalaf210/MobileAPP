# Flutter FCM Notifications & Order Status Guide (2026-05-02)

## General FCM Payload Structure
All order-related notifications include this shared `data` payload:

| Key | Type | Description |
| :--- | :--- | :--- |
| `type` | String | Event identifier (see full list below) |
| `orderId` | String | Unique order ID |
| `customerName` | String | User's full name |
| `userPhone` | String | User's phone number |
| `customerLat` | String | Pickup latitude |
| `customerLng` | String | Pickup longitude |
| `destinationLat` | String | Destination latitude |
| `destinationLng` | String | Destination longitude |
| `price` | String | Expected price |
| `fromPlace` | String | Pickup address |
| `toPlace` | String | Destination address |

---

## 1. Notification Types by `type`
### a. For DRIVERS
| Event Type | Description |
| :--- | :--- |
| `new_order` | A new order is available to all drivers |
| `order_accepted` | Driver successfully accepted the order |
| `order_cancelled` | The order was cancelled by user or system |
| `driver_arrived` | Driver has arrived at pickup location (sent to both user & driver) |
| `trip_started` | The trip has started |
| `trip_completed` | The trip is completed |

### b. For USERS
| Event Type | Description |
| :--- | :--- |
| `order_approved` | Driver has accepted your order |
| `order_cancelled` | Your order was cancelled |
| `driver_arrived` | Driver arrived at your location |
| `trip_started` | Trip started |
| `trip_completed` | Trip completed |

---

## 2. API Changes Recap (Existing)
### Location API
**`GET /api/Location/driver/{driverId}`**
- Now returns **200 OK** with `isOnline: false` if driver exists but no active location.
- **404** only when driver ID is invalid.

### Orders API
- **`GET /api/Orders`**: No longer returns cancelled orders.
- **`PUT /api/Orders/user/cancel`**: New endpoint for passengers to cancel their orders:
  ```json
  {
    "orderId": 123,
    "userId": "user-uuid-here"
  }
  ```

---

## 3. Implementation Tips for Flutter
Use `FirebaseMessaging.onMessage` and `FirebaseMessaging.onMessageOpenedApp` to handle notifications. Parse the `data` payload directly instead of relying only on `notification` fields for reliable handling.
