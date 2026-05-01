# إعداد FCM في الـ Backend

دليل تشغيل خاصية الـ Push Notifications في الـ Backend بعد التعديلات.

---

## 1) ملخص اللي اتعمل

| الملف                                                     | التغيير                                                   |
|-----------------------------------------------------------|-----------------------------------------------------------|
| `Snap.Core/Entities/Driver.cs`                            | أضيف عمود `FcmToken` (nullable)                            |
| `Snap.APIs/DTOs/DriverDto.cs`                             | أضيف `UpdateFcmTokenDto`                                  |
| `Snap.APIs/Controllers/DriverController.cs`               | أضيف endpoint: `PUT /api/Driver/fcm-token`                |
| `Snap.APIs/Services/FcmService.cs`                        | جديد — خدمة إرسال الـ FCM                                  |
| `Snap.APIs/Controllers/OrdersController.cs`               | بيستدعي `IFcmService` بعد إنشاء الاوردر                   |
| `Snap.APIs/Snap.APIs.csproj`                              | أضيف package: `FirebaseAdmin 3.1.0`                       |
| `Snap.APIs/Program.cs`                                    | تسجيل `IFcmService` في DI                                 |
| `Snap.APIs/appsettings.json`                              | أضيف قسم `Fcm:CredentialsPath`                            |
| `Snap.Repository/Migrations/...add driver fcm token.cs`   | Migration جديد                                             |
| `Snap.Repository/Migrations/SnapDbContextModelSnapshot.cs`| تحديث الـ snapshot                                         |

---

## 2) الخطوات اللي محتاج تعملها

### 2.1 تنزيل الـ NuGet packages

```bash
dotnet restore
```

### 2.2 إضافة ملف Firebase Service Account

1. ادخل [Firebase Console](https://console.firebase.google.com/) → اختار مشروعك.
2. **Project Settings** → **Service accounts** → **Generate new private key**.
3. حفظ الملف في `Snap.APIs/firebase-service-account.json`.
4. تأكد إن الـ path في `appsettings.json` صح:

```json
"Fcm": {
  "CredentialsPath": "firebase-service-account.json"
}
```

> ⚠️ **مهم:** ضيف الملف ده في `.gitignore` عشان متقعش الـ credentials في الـ repo.

```gitignore
firebase-service-account.json
```

### 2.3 تشغيل المشروع

```bash
dotnet run --project Snap.APIs
```

عند التشغيل، الـ migration هيتطبق تلقائياً (موجود في `Program.cs`: `dbContext.Database.MigrateAsync()`).

في الـ logs لازم تشوف:

```
FirebaseApp initialized successfully.
```

لو شفت:

```
Firebase credentials file not found at '...'. FCM disabled.
```

يبقى ملف الـ service account مش موجود — راجع خطوة 2.2.

---

## 3) Endpoint جديد للموبايل

### `PUT /api/Driver/fcm-token`

**Body:**

```json
{
  "driverId": 5,
  "fcmToken": "fGxK8d..."
}
```

**Response:** `204 No Content`

**Errors:**
- `404` لو الـ `driverId` مش موجود.

> 💡 لو السواق عمل logout ابعت `fcmToken: null` عشان ميستقبلش notifications.

---

## 4) إزاي الـ flow بيشتغل

```
1. السواق يعمل login في الموبايل
2. الموبايل يجيب FCM token من Firebase
3. الموبايل يبعت PUT /api/Driver/fcm-token
4. Backend يخزن الـ token في عمود Drivers.FcmToken
5. اليوزر يعمل POST /api/Orders
6. OrdersController.CreateOrder يخزن الاوردر
7. FcmService.NotifyDriversOfNewOrderAsync بيتنادى:
   - بيجيب كل السواقين status == "approved" وعندهم FcmToken
   - بيبعت notification multicast لكلهم
   - الـ tokens الباطلة بتتحذف تلقائياً (Unregistered/InvalidArgument)
```

---

## 5) شكل الـ Notification اللي بيتبعت

```json
{
  "notification": {
    "title": "اوردر جديد!",
    "body": "من: [from] → إلى: [to] | [price] جنيه"
  },
  "data": {
    "type": "new_order",
    "orderId": "123",
    "userId": "abc-def",
    "from": "...",
    "to": "...",
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
  },
  "android": {
    "priority": "high",
    "notification": {
      "channel_id": "new_orders_channel",
      "sound": "default"
    }
  },
  "apns": {
    "aps": {
      "sound": "default",
      "content-available": 1
    }
  }
}
```

---

## 6) فلترة الإرسال (دلوقتي)

السيرفر بيبعت بس للسواقين اللي:
- `Status == "approved"`
- عندهم `FcmToken` غير فاضي

### تحسينات مستقبلية ممكنة:

- **Geo filtering:** ابعت بس للسواقين في نطاق X كم من الـ pickup location.
- **Car type filter:** ابعت بس للسواقين اللي عندهم نفس الـ `CarType`.
- **PinkMode:** لو `pinkMode == true`، ابعت بس لسواقات.
- **Online drivers only:** استخدم الـ `_onlineDrivers` من الـ `WebSocketMiddleware`.

كل ده ممكن يضاف لاحقاً في `FcmService.NotifyDriversOfNewOrderAsync`.

---

## 7) Testing سريع

### 7.1 سجل token وهمي

```bash
curl -X PUT http://localhost:5000/api/Driver/fcm-token \
  -H "Content-Type: application/json" \
  -d '{"driverId": 1, "fcmToken": "test_token_123"}'
```

### 7.2 اعمل اوردر

```bash
curl -X POST http://localhost:5000/api/Orders \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user-id-here",
    "date": "2026-05-01T10:00:00Z",
    "from": "المعادي",
    "to": "مدينة نصر",
    "fromLatLng": {"lat": 30.0, "lng": 31.0},
    "toLatLng": {"lat": 30.1, "lng": 31.1},
    "expectedPrice": 50,
    "type": "ride",
    "distance": 5.2,
    "noPassengers": 2,
    "paymentWay": "cash",
    "carType": "sedan",
    "pinkMode": false
  }'
```

في الـ logs لازم تشوف:

```
Order 123: sent X/Y FCM notifications.
```

(ولو الـ token وهمي هيتحذف تلقائياً بعد الفشل)

---

## 8) Troubleshooting

| المشكلة                                      | السبب / الحل                                                  |
|----------------------------------------------|---------------------------------------------------------------|
| `FCM not initialized. Skipping notification` | ملف `firebase-service-account.json` مش موجود أو path غلط       |
| `Failed to initialize FirebaseApp`           | الملف موجود بس JSON ناقص أو غلط — نزّل واحد جديد               |
| `No approved drivers with FCM tokens`        | مفيش سواقين status=approved أو الـ tokens مش متسجلة            |
| الإشعار وصل بس الـ data كله strings          | ده طبيعي. اعمل parse في الموبايل (`int.parse`, `double.parse`) |
| Migration error                              | احذف الـ migration file وشغّل `dotnet ef migrations add`       |
