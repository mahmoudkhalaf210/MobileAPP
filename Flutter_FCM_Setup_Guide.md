# Flutter FCM Setup Guide (2026-05-02)
This guide provides a complete FCM implementation for your ride/order app, with sound, background handling, and Clean Architecture-friendly structure.

---
---

## 1. Add Required Dependencies
Add these to your `pubspec.yaml`:

```yaml
dependencies:
  flutter:
    sdk: flutter
  firebase_core: ^latest
  firebase_messaging: ^latest
  flutter_local_notifications: ^latest
  get_it: ^latest # for service locator
  freezed_annotation: ^latest # optional for models
```

---
---

## 2. Android Setup
### 2.1 AndroidManifest.xml
Add to `android/app/src/main/AndroidManifest.xml`:
```xml
<meta-data
    android:name="com.google.firebase.messaging.default_notification_channel_id"
    android:value="high_importance_channel" />
```

### 2.2 build.gradle
Enable multidex (if needed):
Add to `android/app/build.gradle`:
```gradle
defaultConfig {
    ...
    multiDexEnabled true
}
```

---
---

## 3. iOS Setup
### 3.1 Info.plist
Add to `ios/Runner/Info.plist`:
```xml
<key>FirebaseAppDelegateProxyEnabled</key>
<false/>
<key>UIBackgroundModes</key>
<array>
    <string>remote-notification</string>
</array>
```

---
---

## 4. Service Implementation

### 4.1 NotificationService Class
Create `lib/core/services/notification_service.dart`:
```dart
import 'dart:io';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

@pragma('vm:entry-point')
Future<void> _firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp();
  await NotificationService()._handleMessage(message);
}

class NotificationService {
  static final NotificationService _instance = NotificationService._internal();

  factory NotificationService() {
    return _instance;
  }

  NotificationService._internal();

  final FlutterLocalNotificationsPlugin _localNotifications =
      FlutterLocalNotificationsPlugin();

  final FirebaseMessaging _firebaseMessaging = FirebaseMessaging.instance;

  static const String channelId = 'high_importance_channel';
  static const String channelName = 'High Importance Notifications';
  static const String channelDescription = 'Notifications for ride and order updates';

  Future<void> initialize() async {
    await _requestPermissions();
    await _createNotificationChannel();
    await _setupListeners();
    await _getInitialMessage();
  }

  Future<void> _requestPermissions() async {
    if (Platform.isIOS) {
      await _firebaseMessaging.requestPermission(
        alert: true,
        badge: true,
        sound: true,
        provisional: false,
      );
    } else if (Platform.isAndroid) {
      await _firebaseMessaging.requestPermission(
        alert: true,
        badge: true,
        sound: true,
      );
    }
  }

  Future<void> _createNotificationChannel() async {
    if (Platform.isAndroid) {
      const AndroidNotificationChannel channel = AndroidNotificationChannel(
        channelId,
        channelName,
        description: channelDescription,
        importance: Importance.max,
        playSound: true,
      );

      await _localNotifications
          .resolvePlatformSpecificImplementation<
              AndroidFlutterLocalNotificationsPlugin>()
          ?.createNotificationChannel(channel);
    }
  }

  Future<void> _setupListeners() async {
    FirebaseMessaging.onBackgroundMessage(_firebaseMessagingBackgroundHandler);

    FirebaseMessaging.onMessage.listen((RemoteMessage message) {
      _handleMessage(message, showLocal: true);
    });

    FirebaseMessaging.onMessageOpenedApp.listen(_handleMessageOpened);
  }

  Future<void> _getInitialMessage() async {
    RemoteMessage? initialMessage =
        await _firebaseMessaging.getInitialMessage();
    if (initialMessage != null) {
      _handleMessageOpened(initialMessage);
    }
  }

  static Future<void> _handleMessage(
    RemoteMessage message, {
    bool showLocal = false,
  }) async {
    final data = message.data;
    final type = data['type'] as String?;

    if (showLocal && message.notification != null) {
      await _instance._showLocalNotification(message);
    }

    switch (type) {
      case 'new_order':
        // TODO: Handle new order
        break;
      case 'order_approved':
        // TODO: Handle order approved
        break;
      case 'order_cancelled':
        // TODO: Handle order cancelled
        break;
      case 'driver_arrived':
        // TODO: Handle driver arrived
        break;
      case 'trip_started':
        // TODO: Handle trip started
        break;
      case 'trip_completed':
        // TODO: Handle trip completed
        break;
    }
  }

  Future<void> _showLocalNotification(RemoteMessage message) async {
    const AndroidNotificationDetails androidPlatformChannelSpecifics =
        AndroidNotificationDetails(
      channelId,
      channelName,
      channelDescription: channelDescription,
      importance: Importance.max,
      priority: Priority.high,
      playSound: true,
    );

    const DarwinNotificationDetails iOSPlatformChannelSpecifics =
        DarwinNotificationDetails(
      presentAlert: true,
      presentBadge: true,
      presentSound: true,
    );

    NotificationDetails platformChannelSpecifics = NotificationDetails(
      android: androidPlatformChannelSpecifics,
      iOS: iOSPlatformChannelSpecifics,
    );

    await _localNotifications.show(
      message.hashCode,
      message.notification?.title ?? 'Update',
      message.notification?.body ?? 'New update available',
      platformChannelSpecifics,
      payload: message.data['type'],
    );
  }

  static void _handleMessageOpened(RemoteMessage message) {
    final type = message.data['type'] as String?;
    switch (type) {
      case 'new_order':
        // TODO: Navigate to order details
        break;
      case 'order_approved':
        // TODO: Navigate to trip screen
        break;
      // Add other cases as needed
    }
  }
}
```

---
---

## 5. Initialize in main.dart
```dart
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:get_it/get_it.dart';
import 'core/services/notification_service.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp();
  
  // Initialize services
  GetIt.I.registerSingleton<NotificationService>(NotificationService());
  await GetIt.I<NotificationService>().initialize();

  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Ride App',
      home: const HomePage(),
    );
  }
}

class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Home')),
      body: const Center(child: Text('Ride App')),
    );
  }
}
```

---
---

## 6. FCM Payload Example (Backend -> Flutter)
```json
{
  "token": "fcm_device_token_here",
  "notification": {
    "title": "New Order Available",
    "body": "Check the app for a new trip request!",
    "sound": "default"
  },
  "data": {
    "type": "new_order",
    "orderId": "123",
    "customerName": "Ahmed Ali",
    "userPhone": "01012345678",
    "customerLat": "30.05",
    "customerLng": "31.23",
    "destinationLat": "30.10",
    "destinationLng": "31.35",
    "price": "55.0",
    "fromPlace": "Maadi, Cairo",
    "toPlace": "Heliopolis, Cairo"
  },
  "android": {
    "notification": {
      "channel_id": "high_importance_channel",
      "sound": "default"
    }
  },
  "apns": {
    "payload": {
      "aps": {
        "sound": "default"
      }
    }
  }
}
```

---
---

## 7. Best Practices to Avoid Silent Notifications
1. **Always use data payload first:** Use `data` for logic, only rely on `notification` for display to the user.
2. **Ensure sound field exists in payload:** Include `"sound": "default"` in both `notification` and platform-specific configs.
3. **Always set android_channel_id:** Ensure all Android notifications use the channel ID `"high_importance_channel"`.
4. **Request permissions explicitly on iOS:** Sound is not enabled by default on iOS; always request permissions.
5. **Test all app states:** Make sure to test foreground, background, and terminated states.

