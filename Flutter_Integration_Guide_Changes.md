# Flutter Integration Changes (May 2, 2026)
## Summary of Changes to Implement in Flutter App

---

## 1. Required Dependencies
Add these to your `pubspec.yaml`:
```yaml
dependencies:
  firebase_core: ^latest
  firebase_messaging: ^latest
  flutter_local_notifications: ^latest
  get_it: ^latest
```

---

## 2. Android Setup Steps
### Step 2.1 AndroidManifest.xml
In `android/app/src/main/AndroidManifest.xml`, add inside the `<application>` tag:
```xml
<meta-data
    android:name="com.google.firebase.messaging.default_notification_channel_id"
    android:value="high_importance_channel" />
```

### Step 2.2 Enable Multidex (if needed)
In `android/app/build.gradle`, under `defaultConfig`:
```gradle
multiDexEnabled true
```

---

## 3. iOS Setup Steps
### Step 3.1 Info.plist
In `ios/Runner/Info.plist`, add these entries:
```xml
<key>FirebaseAppDelegateProxyEnabled</key>
<false/>
<key>UIBackgroundModes</key>
<array>
    <string>remote-notification</string>
</array>
```

---

## 4. Notification Service Requirements
Create a notification service class that:
- Initializes Firebase Core and FCM
- Creates an Android notification channel with:
  - ID: `high_importance_channel`
  - Importance: Max
  - Sound enabled
- Requests notification permissions (on iOS, include sound permission)
- Handles all 3 app states:
  - Foreground
  - Background
  - Terminated
- **ALWAYS uses `data` payload for logic (don't only rely on `notification`)**
- Shows local notifications with sound when app is in foreground
- Handles navigation with `FirebaseMessaging.onMessageOpenedApp` and `getInitialMessage()`

---

## 5. Notification Types to Handle
| Event Type | Description | For whom? |
| :--- | :--- | :--- |
| `new_order` | New order available | Drivers |
| `order_approved` | Driver accepted your order | Users |
| `order_accepted` | You successfully accepted an order | Drivers |
| `order_cancelled` | Order was cancelled | Both |
| `driver_arrived` | Driver arrived at pickup | Both |
| `trip_started` | Trip has started | Both |
| `trip_completed` | Trip completed | Both |

---

## 6. Shared FCM Payload Data Structure
All notifications include these fields in `data`:
| Field | Type | Description |
| :--- | :--- | :--- |
| `type` | String | Event identifier (see table above) |
| `orderId` | String | Order ID |
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

## 7. Backend FCM Payload Format
The backend sends:
- Android: Uses channel `high_importance_channel` and `sound: "default"`
- iOS: Uses `sound: "default"` in `aps` payload
- Both platforms: Always send full `data` payload

---

## 8. Best Practices to Avoid Silent Notifications
1. **ALWAYS use `data` for logic:** Never rely only on the `notification` part.
2. **Always include `sound` in payload:** The backend now always sends `sound: "default"`.
3. **Use the correct Android channel:** Always use `high_importance_channel` for Android notifications.
4. **Request permissions explicitly:** On iOS, sound is not enabled by default.
5. **Test all app states:** Make sure to test foreground, background, and terminated.
