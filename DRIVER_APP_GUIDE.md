# دليل تطبيق السواق (Driver App) — Backend Integration

دليل شامل لكل الـ APIs والـ flows اللي تطبيق السواق محتاجها مع الـ Backend.

> 🔗 **ملفات مرتبطة:**
> - [DRIVER_APP_FCM_INTEGRATION.md](DRIVER_APP_FCM_INTEGRATION.md) — تفاصيل دمج Firebase Push Notifications.
> - [ORDER_LIFECYCLE.md](ORDER_LIFECYCLE.md) — دورة حياة الاوردر بشكل عام.

---

## 📑 فهرس

1. [المعلومات الأساسية](#1-المعلومات-الأساسية)
2. [Authentication (تسجيل / دخول)](#2-authentication)
3. [بيانات السواق والعربية](#3-بيانات-السواق-والعربية)
4. [Push Notifications (FCM)](#4-push-notifications-fcm)
5. [الموقع المباشر (Location)](#5-الموقع-المباشر-location)
6. [الاوردرات (Orders)](#6-الاوردرات-orders)
7. [الرحلات السابقة (Trip History)](#7-الرحلات-السابقة)
8. [المحفظة والشحن (Wallet)](#8-المحفظة-والشحن-wallet)
9. [الـ Flow الكامل](#9-الـ-flow-الكامل-من-أول-لآخر)
10. [Models أساسية](#10-models-أساسية)

---

## 1) المعلومات الأساسية

| البيان         | القيمة                                      |
|----------------|---------------------------------------------|
| Base URL       | `https://YOUR_API_DOMAIN`                   |
| Format         | `application/json`                          |
| Auth           | JWT Bearer Token (في Header بعد الـ login) |
| WebSocket      | `wss://YOUR_API_DOMAIN/ws/location`         |

### Headers أساسية

```http
Content-Type: application/json
Authorization: Bearer <token>     // بعد الـ login
```

---

## 2) Authentication

### 2.1 طلب OTP

```http
POST /api/UsersIdentity/SendOtp
Content-Type: application/json

{ "phoneNumber": "01012345678" }
```

**Response:** `{ "message": "OTP sent to WhatsApp for ..." }`

### 2.2 تأكيد OTP

```http
POST /api/UsersIdentity/VerifyOtp

{ "phoneNumber": "01012345678", "otp": "123456" }
```

### 2.3 تسجيل سواق جديد

```http
POST /api/UsersIdentity/Register

{
  "email": "driver@example.com",
  "fullName": "أحمد محمد",
  "phoneNumber": "01012345678",
  "password": "Pass@123",
  "userType": "driver",   // مهم: لازم driver
  "gender": "male"
}
```

**Response:** `UserDto` فيه `userId` و `token`.

> ⚠️ شروط الباسوورد: 6-20 حرف، حرف كبير + رقم + حرف خاص.

### 2.4 تسجيل دخول

```http
POST /api/UsersIdentity/Login

{ "emailOrPhone": "driver@example.com", "password": "Pass@123" }
```

**Response:**

```json
{
  "userId": "abc-def-...",
  "dispalyName": "أحمد محمد",
  "email": "driver@example.com",
  "phoneNumber": "01012345678",
  "token": "eyJhbGc...",
  "userType": "driver",
  "gender": "male"
}
```

### 2.5 نسيت الباسوورد

```
POST /api/UsersIdentity/RequestResetPasswordOtp   { phoneNumber }
POST /api/UsersIdentity/VerifyResetPasswordOtp    { phoneNumber, otp }
POST /api/UsersIdentity/ResetPassword             { email, newPassword }
```

### 2.6 صورة البروفايل

```
PUT /api/UsersIdentity/UpdateImage/{userId}    body: "<base64 string>"
GET /api/UsersIdentity/GetImage/{userId}
```

---

## 3) بيانات السواق والعربية

### 3.1 إنشاء بروفايل سواق (بعد الـ Register)

```http
POST /api/Driver

{
  "driverPhoto": "url",
  "driverIdCard": "url",
  "driverLicenseFront": "url",
  "driverLicenseBack": "url",
  "idCardFront": "url",
  "idCardBack": "url",
  "driverFullname": "أحمد محمد",
  "nationalId": "29012345678901",
  "age": 30,
  "licenseNumber": "ABC123",
  "email": "driver@example.com",
  "password": "Pass@123",
  "licenseExpiryDate": "2030-01-01T00:00:00Z",
  "userId": "<from-register-response>"
}
```

**Response:** `CreateDriver` مع `id` (ده هو **DriverId** اللي هتستخدمه في كل المكالمات الجاية).

> 🔑 **مهم:** فرق بين `userId` (string من AspNetUsers) و `driverId` (int من جدول Drivers).

### 3.2 جلب بيانات السواق

```http
GET /api/Driver/{userId}              // بالـ AspNetUser id
GET /api/Driver/DriverId/{driverId}   // بالـ Driver int id
```

### 3.3 حالة السواق (Approval)

السواق يفضل `status = "pending"` لحد ما الأدمن يعتمده:

```http
GET /api/Driver/pending                            // الأدمن
PUT /api/Driver/{driverId}/status   { status }     // pending | approved | reject
GET /api/Driver/approved                           // كل المعتمدين
```

> ⚠️ **مهم جداً:** الـ Push Notifications للاوردرات بتيجي بس للسواقين `status == "approved"`.

### 3.4 بيانات العربية

```http
POST /api/CarData

{
  "carPhoto": "url",
  "licenseFront": "url",
  "licenseBack": "url",
  "carBrand": "Toyota",
  "carModel": "Corolla",
  "carColor": "Red",
  "plateNumber": "ABC 1234",
  "driverId": 5
}

GET /api/CarData/by-driver/{driverId}
```

---

## 4) Push Notifications (FCM)

السواق هيستقبل الاوردرات الجديدة عن طريق Firebase Cloud Messaging — مش polling.

### 4.1 الإعداد الكامل

راجع [DRIVER_APP_FCM_INTEGRATION.md](DRIVER_APP_FCM_INTEGRATION.md) فيه كل التفاصيل (إعداد Firebase، الـ packages، استقبال الـ notifications في ٣ حالات).

### 4.2 Endpoint تسجيل الـ FCM Token

بعد الـ login مباشرة جيب الـ FCM Token وابعته:

```http
PUT /api/Driver/fcm-token

{ "driverId": 5, "fcmToken": "fGxK8d..." }
```

**Response:** `204 No Content`

> 💡 ابعته كمان لما الـ token يتجدد (`onTokenRefresh`). وعند logout ابعت `null`.

### 4.3 شكل الـ Notification اللي بيوصل

```json
{
  "notification": { "title": "اوردر جديد!", "body": "..." },
  "data": {
    "type": "new_order",
    "orderId": "123",
    "userId": "abc",
    "from": "...", "to": "...",
    "fromLat": "30.0", "fromLng": "31.0",
    "toLat": "30.1", "toLng": "31.1",
    "expectedPrice": "50.0",
    "distance": "5.2",
    "carType": "sedan",
    "paymentWay": "cash",
    "noPassengers": "2",
    "pinkMode": "false"
  }
}
```

> ⚠️ كل قيم الـ `data` بتيجي **String** حتى لو رقم. اعمل parse في الـ app.

---

## 5) الموقع المباشر (Location)

السواق لازم يبعت موقعه باستمرار عشان اليوزر يشوفه على الخريطة.

### 5.1 عبر REST (أسهل)

```http
POST /api/Location/update

{
  "driverId": 5,
  "lat": 30.0444,
  "lng": 31.2357,
  "timestamp": "2026-05-01T10:00:00Z"
}
```

ابعته كل **3-5 ثواني** والسواق online.

### 5.2 عبر WebSocket (الأفضل للأداء)

```
URL: wss://YOUR_API_DOMAIN/ws/location
```

**اتصال السواق:**

```json
{ "action": "ConnectDriver", "driverId": 5 }
```

**تحديث الموقع:**

```json
{
  "action": "UpdateLocation",
  "location": {
    "lat": 30.0444,
    "lng": 31.2357,
    "timestamp": "2026-05-01T10:00:00Z"
  }
}
```

**Heartbeat (كل 30 ثانية):**

```json
{ "action": "Ping" }
```

السيرفر يرد بـ `Pong`.

### 5.3 لما السواق يقفل / يخرج

اعمل WebSocket disconnect عادي (السيرفر هيشيله من الـ online drivers تلقائياً).

أو REST:

```http
DELETE /api/Location/driver/{driverId}
```

---

## 6) الاوردرات (Orders)

### 6.1 جلب الاوردرات المتاحة

```http
GET /api/Orders
```

**Response:** كل الاوردرات. فلتر في الـ app:

```dart
final available = orders.where((o) =>
    o.status == 'pending' && o.driverid == null
).toList();
```

> 💡 **ملاحظة:** الفلترة كلها client-side حالياً. ومع الـ FCM، السواق مش محتاج يعمل polling — هيستقبل الاوردرات لايف.

### 6.2 جلب الاوردرات بتاعتي (المقبولة)

```dart
final myActive = orders.where((o) =>
    o.driverid == myDriverId &&
    (o.status == 'accepted' || o.status == 'in_progress')
).toList();
```

### 6.3 جلب اوردر معين

```http
GET /api/Orders/{id}
```

### 6.4 قبول اوردر

```http
PUT /api/Orders/driver

{
  "orderId": 123,
  "driverid": 5,
  "status": "accepted"
}
```

**Response:** `204 No Content`

### 6.5 إنهاء اوردر

نفس الـ endpoint:

```http
PUT /api/Orders/driver

{ "orderId": 123, "driverid": 5, "status": "completed" }
```

### 6.6 إلغاء اوردر بعد القبول

```http
PUT /api/Orders/driver

{ "orderId": 123, "driverid": 5, "status": "cancelled" }
```

### 6.7 الاوردر بيتلغى تلقائياً

لو الاوردر فضل `pending` لمدة **٤ دقايق** من غير ما حد يقبله، السيرفر بيلغيه تلقائياً (`status = "cancelled"`).

### 6.8 شكل الاوردر (OrderDto)

```json
{
  "id": 123,
  "userId": "abc",
  "date": "2026-05-01T10:00:00Z",
  "from": "المعادي",
  "to": "مدينة نصر",
  "fromLatLng": { "lat": 30.0, "lng": 31.0 },
  "toLatLng": { "lat": 30.1, "lng": 31.1 },
  "expectedPrice": 50.0,
  "type": "ride",            // ride | delivery
  "distance": 5.2,
  "notes": "ملاحظات",
  "noPassengers": 2,
  "userImage": "url",
  "userName": "محمد علي",
  "userPhone": "01098765432",
  "status": "pending",       // pending | accepted | completed | cancelled
  "driverid": null,
  "review": 0,
  "paymentWay": "cash",
  "carType": "sedan",
  "pinkMode": false
}
```

---

## 7) الرحلات السابقة

### 7.1 إضافة رحلة بعد ما تخلص

```http
POST /api/TripsHistory

{
  "review": 5,
  "paymentWay": "cash",
  "from": "المعادي",
  "to": "مدينة نصر",
  "date": "2026-05-01T10:30:00Z",
  "totalTip": 5.0,
  "driverId": 5
}
```

> 💡 لما الاوردر يخلص: حدّث الاوردر `status = "completed"` ثم اعمل POST في `TripsHistory`.

### 7.2 جلب رحلاتي

```http
GET /api/TripsHistory/driver/{userId}      // الـ AspNetUser id (مش DriverId)
GET /api/TripsHistory                       // الكل
GET /api/TripsHistory/{id}                  // واحد
```

---

## 8) المحفظة والشحن (Wallet)

### 8.1 طلب شحن

```http
POST /api/Driver/requestcharge

{
  "driverId": 5,
  "name": "إيصال شحن",
  "image": "url"
}
```

> الأدمن هيراجع ويعتمد أو يرفض.

### 8.2 خصم من المحفظة (للعمولات مثلاً)

```http
PUT /api/Driver/deductwallet

{ "driverId": 5, "amount": 10 }
```

**Errors:** `400` لو الرصيد أقل من المبلغ.

### 8.3 شوف رصيدك

من `GET /api/Driver/{userId}` → الحقل `wallet`.

---

## 9) الـ Flow الكامل من أول لآخر

```
┌─────────────────────────────────────────────────────────────┐
│  1. تسجيل / دخول                                             │
│     POST /api/UsersIdentity/Register أو Login                │
│     ← احفظ token, userId, userType                            │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼ (لو سواق جديد)
┌─────────────────────────────────────────────────────────────┐
│  2. أكمل بياناتك                                             │
│     POST /api/Driver         → احفظ driverId                 │
│     POST /api/CarData                                        │
│     استنى الأدمن يعملك approve                                │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼ (بعد approve)
┌─────────────────────────────────────────────────────────────┐
│  3. سجّل FCM Token                                           │
│     await Firebase.initializeApp()                           │
│     token = await FirebaseMessaging.instance.getToken()      │
│     PUT /api/Driver/fcm-token                                │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│  4. اتصل بالـ WebSocket لإرسال الموقع                         │
│     wss://.../ws/location                                    │
│     { "action": "ConnectDriver", "driverId": 5 }             │
│                                                              │
│     ابدأ Timer كل 5 ثواني يبعت:                                │
│     { "action": "UpdateLocation", "location": {...} }         │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│  5. استنى الاوردرات الجديدة                                   │
│     ← هتيجي عن طريق FCM Push Notification تلقائياً             │
│     - Foreground: FirebaseMessaging.onMessage                │
│     - Background: FirebaseMessaging.onMessageOpenedApp       │
│     - Terminated: FirebaseMessaging.getInitialMessage        │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│  6. اعرض الاوردر للسواق + قبول/رفض                            │
│     لو قبل:                                                   │
│     PUT /api/Orders/driver { orderId, driverid, "accepted" } │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│  7. اعرض خريطة + اتجاه نقطة الاستلام                          │
│     استمر في إرسال موقعك على الـ WebSocket                     │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼ (بعد ما تخلص الرحلة)
┌─────────────────────────────────────────────────────────────┐
│  8. أنهي الاوردر                                             │
│     PUT /api/Orders/driver { orderId, driverid, "completed" }│
│     POST /api/TripsHistory  { ... }                          │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
                 ارجع للخطوة 5
```

---

## 10) Models أساسية

### 10.1 OrderModel (Dart)

```dart
class OrderModel {
  final int id;
  final String userId;
  final DateTime date;
  final String from;
  final String to;
  final LatLng fromLatLng;
  final LatLng toLatLng;
  final double expectedPrice;
  final String type;
  final double distance;
  final String? notes;
  final int noPassengers;
  final String? userImage;
  final String? userName;
  final String? userPhone;
  final String status;
  final int? driverid;
  final double review;
  final String? paymentWay;
  final String carType;
  final bool pinkMode;

  OrderModel.fromJson(Map<String, dynamic> j)
      : id = j['id'],
        userId = j['userId'],
        date = DateTime.parse(j['date']),
        from = j['from'],
        to = j['to'],
        fromLatLng = LatLng.fromJson(j['fromLatLng']),
        toLatLng = LatLng.fromJson(j['toLatLng']),
        expectedPrice = (j['expectedPrice'] as num).toDouble(),
        type = j['type'],
        distance = (j['distance'] as num).toDouble(),
        notes = j['notes'],
        noPassengers = j['noPassengers'],
        userImage = j['userImage'],
        userName = j['userName'],
        userPhone = j['userPhone'],
        status = j['status'],
        driverid = j['driverid'],
        review = (j['review'] as num).toDouble(),
        paymentWay = j['paymentWay'],
        carType = j['carType'],
        pinkMode = j['pinkMode'];
}

class LatLng {
  final double lat, lng;
  LatLng(this.lat, this.lng);
  LatLng.fromJson(Map j)
      : lat = (j['lat'] as num).toDouble(),
        lng = (j['lng'] as num).toDouble();
}
```

### 10.2 OrderModel.fromFcmData (مهم)

```dart
factory OrderModel.fromFcmData(Map<String, dynamic> data) {
  return OrderModel(
    id: int.parse(data['orderId']),
    userId: data['userId'],
    from: data['from'],
    to: data['to'],
    fromLatLng: LatLng(double.parse(data['fromLat']), double.parse(data['fromLng'])),
    toLatLng:   LatLng(double.parse(data['toLat']),   double.parse(data['toLng'])),
    expectedPrice: double.parse(data['expectedPrice']),
    distance: double.parse(data['distance']),
    carType: data['carType'],
    paymentWay: data['paymentWay'],
    noPassengers: int.parse(data['noPassengers']),
    pinkMode: data['pinkMode'].toString().toLowerCase() == 'true',
    // ...
  );
}
```

### 10.3 DriverModel

```dart
class DriverModel {
  final int id;
  final String driverFullname;
  final String email;
  final String userId;
  final String status;          // pending | approved | reject
  final double review;
  final double wallet;
  final String? gender;
  final String? phoneNumber;
  final String? carBrand;
  // ...
}
```

---

## 11) Status Values المرجعية

### Driver Status

| القيمة      | المعنى                              |
|-------------|--------------------------------------|
| `pending`   | لسه مستني الأدمن يعتمد               |
| `approved`  | معتمد ويقدر يستلم اوردرات             |
| `reject`    | الأدمن رفضه                          |

### Order Status

| القيمة      | المعنى                                 |
|-------------|----------------------------------------|
| `pending`   | اتعمل ولسه مفيش سواق قبل                |
| `accepted`  | سواق قبل                                |
| `completed` | الرحلة خلصت                             |
| `cancelled` | اتلغى (تلقائي بعد 4 دقايق أو يدوي)       |

### Order Type

| القيمة     | المعنى       |
|------------|--------------|
| `ride`     | رحلة         |
| `delivery` | توصيل بضاعة |

---

## 12) Checklist للـ Production

- [ ] Firebase project متعمل و الملفات موجودة (`google-services.json` / `GoogleService-Info.plist`).
- [ ] FCM Token بيتسجل بعد كل login.
- [ ] FCM Token بيتجدد على `onTokenRefresh`.
- [ ] FCM Token بيتمسح على logout (`PUT /api/Driver/fcm-token` بـ `null`).
- [ ] WebSocket reconnection logic لو الاتصال انقطع.
- [ ] Location updates كل 3-5 ثواني والسواق online.
- [ ] الإشعار بيشتغل في Foreground/Background/Terminated.
- [ ] الضغط على الإشعار بيفتح شاشة الاوردر.
- [ ] Battery optimization exemption مطلوب من السواق (Android).
- [ ] طلب صلاحية الموقع (Foreground + Background).
- [ ] طلب صلاحية الإشعارات (Android 13+).
- [ ] Auth token متخزن آمن (`flutter_secure_storage`).
- [ ] Refresh strategy لو الـ token expired (currently `DurationInDays: 2`).

---

## 13) ملاحظات مهمة

1. **userId vs driverId:** كل endpoint يستخدم واحد منهم — ركّز كويس.
2. **Orders filtering** كله client-side دلوقتي (مفيش `?status=pending`).
3. **DELETE order بيمسح من الـ DB** — استخدم `PUT /api/Orders/driver` بـ `cancelled` بدالها.
4. **FCM data dictionary** كله Strings — اعمل parse.
5. **Status values lowercase** دايماً.
6. **WebSocket مش بيوصل اوردرات** — بس مواقع. الاوردرات بتيجي على FCM.
7. **السواق لازم status=approved** عشان يستقبل اوردرات.
8. **JWT token مدته يومين** — اعمل refresh أو re-login بعدها.
