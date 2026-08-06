# Swagger Test Guide - New Features

Base URL: `/api`

This document is based on the current backend implementation in:
- [UsersIdentityController.cs](file:///d:/Mobile%20app%20backend/final/Snap.APIs/Controllers/UsersIdentityController.cs)
- [SavedAddress.cs](file:///d:/Mobile%20app%20backend/final/Snap.APIs/Controllers/SavedAddress.cs)
- [OrdersController.cs](file:///d:/Mobile%20app%20backend/final/Snap.APIs/Controllers/OrdersController.cs)
- [OrderService.cs](file:///d:/Mobile%20app%20backend/final/Snap.APIs/Services/OrderService.cs)
- [OrderNotificationService.cs](file:///d:/Mobile%20app%20backend/final/Snap.APIs/Services/OrderNotificationService.cs)
- [ScheduledRideProcessorService.cs](file:///d:/Mobile%20app%20backend/final/Snap.APIs/Services/ScheduledRideProcessorService.cs)

## 1. What Is Implemented

### User Features
- `GET /UsersIdentity/Profile/{userId}`
- `PUT /UsersIdentity/UpdateProfile/{userId}`
- `GET /UsersIdentity/EmergencyContact/{userId}`
- `PUT /UsersIdentity/EmergencyContact/{userId}`

### Favorite Locations
- `GET /SavedAddress/favorites/{userId}`
- `PUT /SavedAddress/favorites/{userId}/{title}`
- `DELETE /SavedAddress/favorites/{userId}/{title}`

### Booking / Scheduling
- `POST /Orders`
- `PUT /Orders/driver`
- `GET /Orders/user/{userId}/scheduled`
- `PUT /Orders/user/cancel`
- `GET /Orders`
- `GET /Orders/{id}`

### Background Scheduled Processing
- Scheduled reminders are processed every 1 minute.
- Orders with status `scheduled_accepted` can send:
  - `scheduled_reminder`
  - `scheduled_starting_soon`

## 2. Current Scheduling Rules

Current values from [appsettings.json](file:///d:/Mobile%20app%20backend/final/Snap.APIs/appsettings.json):

```json
{
  "OrderSettings": {
    "NotificationMode": "AllDrivers",
    "ScheduledDispatchLeadTimeMinutes": 10,
    "ScheduledCancelCutoffMinutes": 15,
    "ScheduledReminderMinutes": 60,
    "ScheduledStartingSoonMinutes": 10,
    "ScheduledConflictWindowMinutes": 60
  }
}
```

Meaning:
- If `Date > now + 10 minutes` then order is created as `scheduled`
- Otherwise order is created as `pending`
- Scheduled cancel is allowed only before `trip time - 15 minutes`
- Reminder goes to accepted driver when trip is within 60 minutes
- Starting soon changes `scheduled_accepted` to `pending` when trip is within 10 minutes

## 3. Swagger Test JSON

### 3.1 Get User Profile
`GET /api/UsersIdentity/Profile/{userId}`

Example:
```text
/api/UsersIdentity/Profile/1a7077b1-c951-445f-8383-70f7145f3f95
```

Expected response:
```json
{
  "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
  "fullName": "Ahmed Ali",
  "phoneNumber": "01012345678",
  "email": "ahmed@test.com",
  "image": "",
  "userType": "user",
  "gender": "male",
  "emergencyContact": {
    "name": "Mohamed Ali",
    "phoneNumber": "01099999999"
  }
}
```

### 3.2 Update User Profile
`PUT /api/UsersIdentity/UpdateProfile/{userId}`

Body:
```json
{
  "fullName": "Ahmed Ali Updated",
  "phoneNumber": "01022223333",
  "image": "https://example.com/profile.jpg"
}
```

Expected response:
```json
{
  "message": "User profile updated successfully."
}
```

Notes:
- Any field can be omitted.
- `phoneNumber` must be unique.

### 3.3 Get Emergency Contact
`GET /api/UsersIdentity/EmergencyContact/{userId}`

Expected response:
```json
{
  "name": "Mohamed Ali",
  "phoneNumber": "01099999999"
}
```

### 3.4 Save or Update Emergency Contact
`PUT /api/UsersIdentity/EmergencyContact/{userId}`

Body:
```json
{
  "name": "Mohamed Ali",
  "phoneNumber": "01099999999"
}
```

Expected response:
```json
{
  "message": "Emergency contact saved successfully."
}
```

Remove a field by sending empty or null:
```json
{
  "name": "",
  "phoneNumber": null
}
```

## 4. Favorite Locations Swagger Tests

### 4.1 Get Favorites
`GET /api/SavedAddress/favorites/{userId}`

Expected response:
```json
{
  "home": {
    "id": 1,
    "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
    "title": "Home",
    "addressLine": "Maadi, Cairo",
    "latitude": 29.9602,
    "longitude": 31.2569,
    "createdAt": "2026-05-03T10:00:00Z",
    "usageCount": 0
  },
  "work": {
    "id": 2,
    "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
    "title": "Work",
    "addressLine": "Nasr City, Cairo",
    "latitude": 30.0626,
    "longitude": 31.2497,
    "createdAt": "2026-05-03T10:05:00Z",
    "usageCount": 0
  }
}
```

### 4.2 Save or Update Favorite
`PUT /api/SavedAddress/favorites/{userId}/{title}`

Allowed `title`:
- `home`
- `work`

Body:
```json
{
  "addressLine": "Maadi, Cairo",
  "latitude": 29.9602,
  "longitude": 31.2569
}
```

Expected response:
```json
{
  "id": 1,
  "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
  "title": "Home",
  "addressLine": "Maadi, Cairo",
  "latitude": 29.9602,
  "longitude": 31.2569,
  "createdAt": "2026-05-03T10:00:00Z",
  "usageCount": 0
}
```

### 4.3 Delete Favorite
`DELETE /api/SavedAddress/favorites/{userId}/{title}`

Expected response:
```json
{
  "message": "Favorite location deleted successfully."
}
```

## 5. Order Swagger Tests

### 5.1 Create Immediate Order
`POST /api/Orders`

Use a time within the next 10 minutes so it becomes `pending`.

Body:
```json
{
  "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
  "date": "2026-05-03T13:10:00Z",
  "from": "Maadi, Cairo",
  "to": "Heliopolis, Cairo",
  "fromLatLng": {
    "lat": 29.9602,
    "lng": 31.2569
  },
  "toLatLng": {
    "lat": 30.0910,
    "lng": 31.3260
  },
  "expectedPrice": 75.0,
  "type": "ride",
  "distance": 18.5,
  "notes": "Call before arrival",
  "noPassengers": 1,
  "paymentWay": "cash",
  "carType": "standard",
  "pinkMode": false,
  "fcmToken": "user_fcm_token_here"
}
```

Expected behavior:
- Order is saved with status `pending`
- Drivers are notified as `new_order`

Example response:
```json
{
  "id": 101,
  "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
  "date": "2026-05-03T13:10:00Z",
  "from": "Maadi, Cairo",
  "to": "Heliopolis, Cairo",
  "fromLatLng": {
    "lat": 29.9602,
    "lng": 31.2569
  },
  "toLatLng": {
    "lat": 30.0910,
    "lng": 31.3260
  },
  "expectedPrice": 75.0,
  "type": "ride",
  "distance": 18.5,
  "notes": "Call before arrival",
  "noPassengers": 1,
  "userImage": "",
  "userName": "Ahmed Ali",
  "userPhone": "01012345678",
  "status": "pending",
  "driverid": null,
  "review": 0,
  "paymentWay": "cash",
  "carType": "standard",
  "pinkMode": false,
  "fcmToken": "user_fcm_token_here"
}
```

### 5.2 Create Scheduled Order
`POST /api/Orders`

Use a time more than 10 minutes in the future so it becomes `scheduled`.

Body:
```json
{
  "userId": "1a7077b1-c951-445f-8383-70f7145f3f95",
  "date": "2026-05-03T15:30:00Z",
  "from": "Maadi, Cairo",
  "to": "New Cairo",
  "fromLatLng": {
    "lat": 29.9602,
    "lng": 31.2569
  },
  "toLatLng": {
    "lat": 30.0284,
    "lng": 31.4913
  },
  "expectedPrice": 95.0,
  "type": "ride",
  "distance": 26.4,
  "notes": "Scheduled trip",
  "noPassengers": 2,
  "paymentWay": "wallet",
  "carType": "standard",
  "pinkMode": false,
  "fcmToken": "user_fcm_token_here"
}
```

Expected behavior:
- Order is saved with status `scheduled`
- Drivers are notified as `scheduled_order`

### 5.3 Driver Accepts Immediate Order
`PUT /api/Orders/driver`

Body:
```json
{
  "orderId": 101,
  "driverid": 8,
  "status": "approved"
}
```

Expected behavior:
- Status becomes `approved`
- User gets `order_approved`
- Driver gets `order_accepted`

### 5.4 Driver Accepts Scheduled Order
`PUT /api/Orders/driver`

Body:
```json
{
  "orderId": 102,
  "driverid": 8,
  "status": "scheduled_accepted"
}
```

Expected behavior:
- Status becomes `scheduled_accepted`
- Driver assigned to order
- Time conflict check is applied using `ScheduledConflictWindowMinutes`
- User gets `scheduled_order_accepted`
- Driver gets `scheduled_order_reserved`

### 5.5 Driver Arrived
`PUT /api/Orders/driver`

Body:
```json
{
  "orderId": 101,
  "driverid": 8,
  "status": "arrived"
}
```

Expected behavior:
- Status becomes `arrived`
- User and driver get `driver_arrived`

### 5.6 Trip Started
`PUT /api/Orders/driver`

Body:
```json
{
  "orderId": 101,
  "driverid": 8,
  "status": "started"
}
```

Expected behavior:
- Status becomes `started`
- User and driver get `trip_started`

### 5.7 Trip Completed
`PUT /api/Orders/driver`

Body:
```json
{
  "orderId": 101,
  "driverid": 8,
  "status": "complete"
}
```

Expected behavior:
- Status becomes `complete`
- User and driver get `trip_completed`

### 5.8 Driver Cancels Order
`PUT /api/Orders/driver`

Body:
```json
{
  "orderId": 101,
  "driverid": 8,
  "status": "cancel"
}
```

Expected behavior:
- Status becomes `cancel`
- User and driver get `order_cancelled`

### 5.9 User Cancels Order
`PUT /api/Orders/user/cancel`

Body:
```json
{
  "orderId": 101,
  "userId": "1a7077b1-c951-445f-8383-70f7145f3f95"
}
```

Expected behavior:
- If normal ride: cancel succeeds unless already complete
- If scheduled ride: cancel only allowed before `Date - 15 minutes`
- User and driver get `order_cancelled`

### 5.10 Get Scheduled Orders for User
`GET /api/Orders/user/{userId}/scheduled`

Example:
```text
/api/Orders/user/1a7077b1-c951-445f-8383-70f7145f3f95/scheduled
```

Returns orders where:
- `scheduled`
- `scheduled_accepted`
- `pending` with future `Date`

### 5.11 Get All Active Orders
`GET /api/Orders`

Behavior:
- Returns all orders except cancelled ones

### 5.12 Get Order By Id
`GET /api/Orders/{id}`

Example:
```text
/api/Orders/101
```

## 6. Notification Types Currently Sent

Current notification types used by backend:
- `new_order`
- `scheduled_order`
- `order_approved`
- `order_accepted`
- `scheduled_order_accepted`
- `scheduled_order_reserved`
- `scheduled_reminder`
- `scheduled_starting_soon`
- `driver_arrived`
- `trip_started`
- `trip_completed`
- `order_cancelled`

## 7. Important Notes

- Current notification mode is `AllDrivers`, so all drivers with saved FCM token can receive new order notifications.
- Scheduled background processing is not a Swagger endpoint; it is automatic every 1 minute.
- To test scheduled reminder quickly, create a scheduled trip within 60 minutes and accept it with a driver.
- To test `scheduled_starting_soon`, create a scheduled trip within 10 minutes after driver acceptance.
- Emergency contact is stored in Identity Claims, not in a dedicated table.
- Favorite titles only accept `home` or `work` in route, and backend normalizes them to `Home` and `Work`.
