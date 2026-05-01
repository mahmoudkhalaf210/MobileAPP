# دمج Firebase Cloud Messaging (FCM) في تطبيق السواق

دليل كامل للـ Flutter dev بتاع تطبيق السواق عشان يستقبل الاوردرات الجديدة كـ Push Notifications حتى لما التطبيق مقفول.

---

## 1) الفكرة العامة

```
┌─────────────────┐         ┌──────────────┐         ┌──────────────┐
│   USER APP      │  POST   │   BACKEND    │  Send   │   FIREBASE   │
│  Create Order   │ ──────► │  Save Order  │ ──────► │     FCM      │
└─────────────────┘         └──────────────┘         └──────┬───────┘
                                                            │
                                                            │ Push
                                                            ▼
                                                   ┌─────────────────┐
                                                   │   DRIVER APP    │
                                                   │  Show Notif +   │
                                                   │  Open Order     │
                                                   └─────────────────┘
```

اللي محتاج يحصل في تطبيق السواق:

1. السواق يـ login.
2. التطبيق يجيب الـ **FCM Token** من Firebase.
3. يبعت الـ Token لـ Backend عشان يتخزن جنب بيانات السواق.
4. لما يتعمل اوردر جديد، Backend يبعت Push Notification لكل السواقين.
5. التطبيق يستقبل الـ Notification ويعرضها (سواء فاتح أو مقفول).
6. لما السواق يدوس على الـ Notification → يفتح شاشة الاوردر.

---

## 2) إعداد Firebase في المشروع

### 2.1 إنشاء مشروع Firebase

1. ادخل [console.firebase.google.com](https://console.firebase.google.com).
2. اعمل مشروع جديد (مثلاً: `snap-driver-app`).
3. ضيف تطبيق Android و iOS.

### 2.2 ملفات الإعداد

- **Android:** نزّل `google-services.json` وحطه في `android/app/`.
- **iOS:** نزّل `GoogleService-Info.plist` وحطه في `ios/Runner/` عن طريق Xcode.

### 2.3 Packages المطلوبة في `pubspec.yaml`

```yaml
dependencies:
  firebase_core: ^latest
  firebase_messaging: ^latest
  flutter_local_notifications: ^latest
  http: ^latest
  shared_preferences: ^latest
```

### 2.4 إعداد Android

في `android/app/build.gradle`:

```gradle
defaultConfig {
    minSdkVersion 21    // مهم: FCM يحتاج 21+
}
```

في `android/app/src/main/AndroidManifest.xml` ضيف Permissions:

```xml
<uses-permission android:name="android.permission.INTERNET"/>
<uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>
<uses-permission android:name="android.permission.WAKE_LOCK"/>
<uses-permission android:name="android.permission.VIBRATE"/>
```

### 2.5 إعداد iOS

في Xcode:
- فعّل **Push Notifications** في Capabilities.
- فعّل **Background Modes** → Background fetch + Remote notifications.

---

## 3) تهيئة Firebase في الكود

### 3.1 في `main.dart`

```dart
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

// لازم تكون top-level function (مش جوه class)
@pragma('vm:entry-point')
Future<void> _firebaseBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp();
  print('Background message: ${message.messageId}');
  // هنا ممكن تخزن الاوردر في local storage عشان يظهر لما التطبيق يفتح
}

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp();

  // Register background handler
  FirebaseMessaging.onBackgroundMessage(_firebaseBackgroundHandler);

  runApp(MyApp());
}
```

### 3.2 طلب صلاحية الإشعارات

```dart
Future<void> requestNotificationPermission() async {
  final messaging = FirebaseMessaging.instance;

  final settings = await messaging.requestPermission(
    alert: true,
    badge: true,
    sound: true,
  );

  print('Permission status: ${settings.authorizationStatus}');
}
```

استدعيها بعد ما السواق يـ login.

---

## 4) جلب الـ FCM Token وإرساله للـ Backend

### 4.1 جلب الـ Token

```dart
Future<String?> getFcmToken() async {
  final messaging = FirebaseMessaging.instance;
  final token = await messaging.getToken();
  print('FCM Token: $token');
  return token;
}
```

### 4.2 إرسال الـ Token للـ Backend

> **Endpoint:** `PUT /api/Drivers/fcm-token`
> **Body:**
> ```json
> {
>   "driverId": 5,
>   "fcmToken": "fGxK8d...الـ Token الطويل"
> }
> ```
> **Response:** `204 No Content`

```dart
import 'package:http/http.dart' as http;
import 'dart:convert';

Future<void> sendFcmTokenToBackend(int driverId, String token) async {
  final response = await http.put(
    Uri.parse('https://YOUR_API/api/Drivers/fcm-token'),
    headers: {'Content-Type': 'application/json'},
    body: jsonEncode({
      'driverId': driverId,
      'fcmToken': token,
    }),
  );

  if (response.statusCode != 204) {
    print('Failed to register FCM token: ${response.body}');
  }
}
```

### 4.3 متى تبعت الـ Token؟

1. **بعد الـ login مباشرة:**

```dart
Future<void> onLoginSuccess(int driverId) async {
  final token = await getFcmToken();
  if (token != null) {
    await sendFcmTokenToBackend(driverId, token);
  }
}
```

2. **لما الـ Token يتجدد** (مهم جداً):

```dart
FirebaseMessaging.instance.onTokenRefresh.listen((newToken) async {
  final driverId = await getStoredDriverId();
  if (driverId != null) {
    await sendFcmTokenToBackend(driverId, newToken);
  }
});
```

3. **عند الـ logout** ابعت `null` أو endpoint مخصص لمسح الـ Token عشان السواق ميستقبلش اوردرات بعد الخروج.

---

## 5) شكل الـ Notification اللي هيوصل من السيرفر

السيرفر هيبعت notification بالشكل ده:

```json
{
  "notification": {
    "title": "اوردر جديد!",
    "body": "من: المعادي → إلى: مدينة نصر | 50 جنيه"
  },
  "data": {
    "type": "new_order",
    "orderId": "123",
    "userId": "abc-def",
    "from": "المعادي",
    "to": "مدينة نصر",
    "fromLat": "30.0",
    "fromLng": "31.0",
    "toLat": "30.1",
    "toLng": "31.1",
    "expectedPrice": "50.0",
    "distance": "5.2",
    "carType": "sedan",
    "paymentWay": "cash",
    "noPassengers": "2",
    "pinkMode": "false"
  }
}
```

> 🔑 **مهم:** كل القيم في `data` بتيجي كـ **String** حتى لو Number. اعمل `int.parse` / `double.parse` لما تحتاج.

---

## 6) استقبال الـ Notification في ٣ حالات

### 6.1 التطبيق فاتح (Foreground)

```dart
FirebaseMessaging.onMessage.listen((RemoteMessage message) {
  print('Foreground notification received');
  print('Title: ${message.notification?.title}');
  print('Data: ${message.data}');

  if (message.data['type'] == 'new_order') {
    final order = OrderModel.fromFcmData(message.data);
    showLocalNotificationWithSound(order);
    addOrderToList(order);
  }
});
```

> 💡 في Foreground الـ system مش بيعرض الـ notification تلقائياً — لازم تستخدم `flutter_local_notifications` لو عايز تعرضها.

### 6.2 التطبيق في الـ Background

```dart
FirebaseMessaging.onMessageOpenedApp.listen((RemoteMessage message) {
  // السواق دوس على الـ notification والتطبيق فتح
  if (message.data['type'] == 'new_order') {
    final orderId = int.parse(message.data['orderId']);
    Navigator.pushNamed(context, '/order-details', arguments: orderId);
  }
});
```

### 6.3 التطبيق مقفول تماماً (Terminated)

```dart
final initialMessage = await FirebaseMessaging.instance.getInitialMessage();
if (initialMessage != null && initialMessage.data['type'] == 'new_order') {
  final orderId = int.parse(initialMessage.data['orderId']);
  Navigator.pushNamed(context, '/order-details', arguments: orderId);
}
```

استدعي ده في `initState` لأول شاشة بعد الـ login.

---

## 7) Local Notifications للـ Foreground

عشان تعرض الـ notification بصوت عالي حتى لو التطبيق مفتوح:

```dart
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

final FlutterLocalNotificationsPlugin _localNotif = FlutterLocalNotificationsPlugin();

Future<void> initLocalNotifications() async {
  const android = AndroidInitializationSettings('@mipmap/ic_launcher');
  const ios = DarwinInitializationSettings();
  await _localNotif.initialize(
    InitializationSettings(android: android, iOS: ios),
    onDidReceiveNotificationResponse: (response) {
      // المستخدم دوس على الـ notification
      if (response.payload != null) {
        final orderId = int.parse(response.payload!);
        // navigate to order screen
      }
    },
  );
}

Future<void> showLocalNotificationWithSound(OrderModel order) async {
  const androidDetails = AndroidNotificationDetails(
    'new_orders_channel',
    'New Orders',
    channelDescription: 'إشعارات الاوردرات الجديدة',
    importance: Importance.max,
    priority: Priority.high,
    sound: RawResourceAndroidNotificationSound('order_alert'),
    playSound: true,
  );
  const iosDetails = DarwinNotificationDetails(presentSound: true);

  await _localNotif.show(
    order.id,
    'اوردر جديد!',
    'من: ${order.from} → إلى: ${order.to}',
    NotificationDetails(android: androidDetails, iOS: iosDetails),
    payload: order.id.toString(),
  );
}
```

> 💡 ضيف ملف صوت `order_alert.mp3` في `android/app/src/main/res/raw/` لو عايز صوت مخصص.

---

## 8) Model للـ Order من بيانات الـ FCM

```dart
class OrderModel {
  final int id;
  final String userId;
  final String from;
  final String to;
  final double fromLat;
  final double fromLng;
  final double toLat;
  final double toLng;
  final double expectedPrice;
  final double distance;
  final String carType;
  final String paymentWay;
  final int noPassengers;
  final bool pinkMode;

  OrderModel({
    required this.id,
    required this.userId,
    required this.from,
    required this.to,
    required this.fromLat,
    required this.fromLng,
    required this.toLat,
    required this.toLng,
    required this.expectedPrice,
    required this.distance,
    required this.carType,
    required this.paymentWay,
    required this.noPassengers,
    required this.pinkMode,
  });

  factory OrderModel.fromFcmData(Map<String, dynamic> data) {
    return OrderModel(
      id: int.parse(data['orderId']),
      userId: data['userId'],
      from: data['from'],
      to: data['to'],
      fromLat: double.parse(data['fromLat']),
      fromLng: double.parse(data['fromLng']),
      toLat: double.parse(data['toLat']),
      toLng: double.parse(data['toLng']),
      expectedPrice: double.parse(data['expectedPrice']),
      distance: double.parse(data['distance']),
      carType: data['carType'],
      paymentWay: data['paymentWay'],
      noPassengers: int.parse(data['noPassengers']),
      pinkMode: data['pinkMode'].toString().toLowerCase() == 'true',
    );
  }
}
```

---

## 9) Flow الكامل خطوة بخطوة في تطبيق السواق

```
1. App opens
       │
       ▼
2. Firebase.initializeApp()
       │
       ▼
3. السواق يعمل Login
       │
       ▼
4. requestNotificationPermission()
       │
       ▼
5. token = await getFcmToken()
       │
       ▼
6. PUT /api/Drivers/fcm-token { driverId, fcmToken: token }
       │
       ▼
7. Setup listeners:
   - FirebaseMessaging.onMessage          (Foreground)
   - FirebaseMessaging.onMessageOpenedApp (Background tap)
   - FirebaseMessaging.getInitialMessage  (Terminated tap)
   - FirebaseMessaging.onTokenRefresh     (Token renewal)
       │
       ▼
8. السواق مستني الاوردرات:
   - لو فتح التطبيق → الاوردر يجي عبر onMessage + Local Notification
   - لو مقفول → نظام التشغيل يعرض الـ notification
   - لما يدوس → التطبيق يفتح على شاشة الاوردر
       │
       ▼
9. السواق يقبل أو يرفض:
   PUT /api/Orders/driver { orderId, driverid, status: "accepted" }
```

---

## 10) Checklist قبل الـ Production

- [ ] `google-services.json` موجود في `android/app/`.
- [ ] `GoogleService-Info.plist` موجود في `ios/Runner/`.
- [ ] Push Notifications + Background Modes مفعلين في iOS.
- [ ] APNs Key مرفوع على Firebase Console (لـ iOS).
- [ ] الـ FCM Token بيتبعت بعد الـ login وعند الـ refresh.
- [ ] الإشعار بيشتغل في الـ ٣ حالات (Foreground / Background / Terminated).
- [ ] Local notification بصوت في الـ Foreground.
- [ ] الضغط على الإشعار بيفتح شاشة الاوردر.
- [ ] الـ FCM Token بيتمسح عند الـ logout.
- [ ] اختبار على جهاز حقيقي مش Emulator (مهم لـ iOS بالذات).

---

## 11) ملاحظات مهمة

1. **iOS Simulator مش بيدعم Push Notifications** — اختبر على جهاز iPhone حقيقي.
2. **Token بيتغير** أحياناً — لازم تتعامل مع `onTokenRefresh`.
3. **Battery optimization** على Android بيقفل الـ background — اطلب من السواق يعفي التطبيق من الـ optimization.
4. **iOS Silent push** له قيود — لازم تبعت `notification` payload مش بس `data`.
5. **حجم الـ data في الـ Notification** ميزدش عن **4KB** total.

---

## 12) لو فيه مشكلة

| المشكلة                                | الحل                                                              |
|----------------------------------------|-------------------------------------------------------------------|
| الإشعار مش بيوصل خالص                  | تأكد إن الـ FCM Token اتسجل صح في الـ Backend                     |
| بيوصل بس التطبيق مفتوح                 | متستخدمش الـ silent push، استخدم `notification` payload           |
| في Android بس مش في iOS                | تأكد من APNs Key على Firebase Console                             |
| الـ Token null                          | تأكد من `Firebase.initializeApp()` قبل أي كول للـ messaging       |
| الإشعار بيوصل بس مفيش صوت              | استخدم `flutter_local_notifications` في الـ Foreground            |
| الضغط على الإشعار مش بيفتح شاشة معينة  | تأكد من `getInitialMessage()` في الـ initState                    |
