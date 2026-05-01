# دورة حياة الاوردر (Order Lifecycle)

شرح كامل للسيكل الخاص بالاوردرات في الـ Backend عشان تطبيق اليوزر وتطبيق السواق.

---

## 1) الـ Statuses المتاحة للاوردر

الاوردر بيمر بأربع حالات أساسية موجودين في حقل `Status`:

| Status      | المعنى                                                                 |
|-------------|------------------------------------------------------------------------|
| `pending`   | الاوردر اتعمل من اليوزر ولسه مفيش سواق قبله. (Default عند الإنشاء)      |
| `accepted`  | سواق قبل الاوردر وبقى مرتبط بيه (`Driverid` مش null).                   |
| `cancelled` | الاوردر اتلغى. ممكن يبقى cancellation من السيستم لو عدى ٤ دقايق pending.|
| `completed` | الاوردر خلص (لما السواق ينهي الرحلة).                                   |

> الـ Status بيتحط بقيمة `pending` تلقائيًا عند إنشاء الاوردر — راجع [OrdersController.cs:56](Snap.APIs/Controllers/OrdersController.cs#L56).

---

## 2) السيكل الكامل خطوة بخطوة

```
┌──────────────────────┐
│  USER APP            │
│  POST /api/Orders    │  ← اليوزر بيعمل اوردر، Status = "pending"
└──────────┬───────────┘
           │
           ▼
┌──────────────────────────────────────────┐
│  الاوردر متاح للسواقين                     │
│  DRIVER APP بيعمل GET /api/Orders         │
│  ويفلتر على status == "pending"           │
└──────────┬───────────────────────────────┘
           │
           ├──── سواق قبل الاوردر ──────────────────┐
           │                                        │
           │    PUT /api/Orders/driver              │
           │    { OrderId, Driverid, Status }       │
           │    Status = "accepted"                 │
           │                                        ▼
           │                               ┌──────────────────┐
           │                               │  الرحلة شغالة     │
           │                               │  (location WS)    │
           │                               └────────┬─────────┘
           │                                        │
           │                                        ▼
           │                               PUT /api/Orders/driver
           │                               Status = "completed"
           │                                        │
           │                                        ▼
           │                               POST /api/TripsHistory
           │                               POST /api/UserHistory
           │
           └──── ٤ دقايق عدت ولا حد قبل ──────► Status = "cancelled" (تلقائي)
                 (OrderCancellationService)
```

---

## 3) الـ Endpoints بالتفصيل

### 3.1 إنشاء اوردر — User App

- **Method:** `POST`
- **URL:** `/api/Orders`
- **Body:**

```json
{
  "userId": "string",
  "date": "2026-05-01T10:00:00Z",
  "from": "العنوان من",
  "to": "العنوان إلى",
  "fromLatLng": { "lat": 30.0, "lng": 31.0 },
  "toLatLng":   { "lat": 30.1, "lng": 31.1 },
  "expectedPrice": 50.0,
  "type": "ride",          // ride | delivery
  "distance": 5.2,
  "notes": "ملاحظات",
  "noPassengers": 2,
  "paymentWay": "cash",
  "carType": "sedan",
  "pinkMode": false
}
```

- **Response 200:** الاوردر كامل، الـ `status` هيرجع `"pending"` والـ `driverid` = `null`.
- **Errors:** `400` لو الـ payload أو `type` ناقص، `404` لو الـ `userId` مش موجود.

> الكود: [OrdersController.cs:25-92](Snap.APIs/Controllers/OrdersController.cs#L25-L92).

---

### 3.2 جلب كل الاوردرات — Driver App

- **Method:** `GET`
- **URL:** `/api/Orders`
- **Response:** ليست كل الاوردرات بكل الـ statuses.

> الكود: [OrdersController.cs:110-141](Snap.APIs/Controllers/OrdersController.cs#L110-L141).

#### فلترة في تطبيق السواق (Client-side)

السواق محتاج يشوف بس الـ pending orders اللي مفيهاش سواق:

```dart
final pending = orders
    .where((o) => o.status == 'pending' && o.driverid == null)
    .toList();
```

ولو السواق عايز يشوف الاوردر اللي قابله:

```dart
final myActive = orders
    .where((o) => o.driverid == myDriverId &&
                  (o.status == 'accepted' || o.status == 'in_progress'))
    .toList();
```

> ⚠️ **ملاحظة مهمة:** حاليًا مفيش endpoint مخصص لجلب الاوردرات حسب الـ status أو حسب السواق. الفلترة كلها بتحصل في الموبايل. لو فيه ضغط على الـ API ممكن نضيف:
> - `GET /api/Orders?status=pending`
> - `GET /api/Orders/driver/{driverId}`

---

### 3.3 جلب اوردر بالـ ID — Both Apps

- **Method:** `GET`
- **URL:** `/api/Orders/{id}`
- **Response:** بيانات الاوردر كاملة، أو `404` لو مش موجود.

> الكود: [OrdersController.cs:144-177](Snap.APIs/Controllers/OrdersController.cs#L144-L177).

---

### 3.4 السواق يقبل الاوردر / يحدّث حالته — Driver App

- **Method:** `PUT`
- **URL:** `/api/Orders/driver`
- **Body:**

```json
{
  "orderId": 123,
  "driverid": 5,
  "status": "accepted"   // accepted | completed | cancelled
}
```

- **Response 204:** No Content.
- **Errors:** `404` لو الاوردر مش موجود.

> الكود: [OrdersController.cs:95-107](Snap.APIs/Controllers/OrdersController.cs#L95-L107).

#### ملاحظات على الـ Statuses من جهة السواق

- لما السواق يقبل: `status = "accepted"` + `driverid = <id>`.
- لما الرحلة تخلص: `status = "completed"`.
- لو السواق لغى بعد ما قبل: `status = "cancelled"`.

> ⚠️ حاليًا الـ endpoint مش بيعمل validation على الـ status string — أي قيمة هتترفع. اتفق مع الـ Flutter dev على القيم الثابتة (`pending`, `accepted`, `completed`, `cancelled`).

---

### 3.5 إلغاء/حذف الاوردر — User App

- **Method:** `DELETE`
- **URL:** `/api/Orders/{id}`
- **Response 204:** No Content.

> الكود: [OrdersController.cs:180-190](Snap.APIs/Controllers/OrdersController.cs#L180-L190).

> ⚠️ **تنبيه:** الـ DELETE ده بيمسح الاوردر من الـ DB نهائيًا، مش بيغيّر الـ status لـ `cancelled`. لو عايز السواق لسه يشوف الـ history، استخدم بدالها:
>
> `PUT /api/Orders/driver` مع `status = "cancelled"`.

---

### 3.6 الإلغاء التلقائي بعد ٤ دقايق

في خدمة Background Service بتشتغل كل دقيقة وتلاقي الاوردرات اللي:
- `status == "pending"`
- مر عليها أكتر من **٤ دقايق** من تاريخ الإنشاء (`Date`)

وبتحوّل الـ `status` تلقائيًا لـ `"cancelled"`.

> الكود: [OrderCancellationService.cs:48-72](Snap.APIs/Services/OrderCancellationService.cs#L48-L72).

> 👈 يعني تطبيق اليوزر لازم يعمل polling أو يفتح WebSocket عشان يعرف لو الاوردر اتلغى تلقائيًا.

---

## 4) بعد ما الرحلة تخلص

### 4.1 سجل الرحلة للسواق — Driver App

- **Method:** `POST`
- **URL:** `/api/TripsHistory`
- **Body:**

```json
{
  "review": 4.5,
  "paymentWay": "cash",
  "from": "...",
  "to": "...",
  "date": "2026-05-01T10:30:00Z",
  "totalTip": 5.0,
  "driverId": 5
}
```

- **GET كل رحلات سواق:** `GET /api/TripsHistory/driver/{userId}` — لاحظ هنا الـ `userId` هو الـ AspNetUser id مش الـ DriverId.

> الكود: [TripsHistoryController.cs:106-130](Snap.APIs/Controllers/TripsHistoryController.cs#L106-L130).

### 4.2 سجل الرحلة لليوزر — User App

- **Method:** `POST`
- **URL:** `/api/UserHistory`
- **Body:**

```json
{
  "userId": "string",
  "from": "...",
  "to": "...",
  "price": 50.0,
  "date": "2026-05-01T10:30:00Z",
  "paymentMethod": "cash",
  "rideType": "ride"
}
```

- **GET كل رحلات يوزر:** `GET /api/UserHistory/user/{userId}`.

> الكود: [UserHistoryController.cs](Snap.APIs/Controllers/UserHistoryController.cs).

---

## 5) الـ WebSocket (Real-time Location)

موازي للـ REST API فيه WebSocket على المسار `/ws/location`. مالوش علاقة مباشرة بإنشاء/إلغاء الاوردر، بس بيستخدم لتتبع موقع السواق على الخريطة.

### Actions اللي تطبيق السواق يبعتها:

```json
{ "action": "ConnectDriver", "driverId": 5 }

{ "action": "UpdateLocation",
  "location": { "lat": 30.0, "lng": 31.0, "timestamp": "2026-05-01T10:00:00Z" } }
```

### Actions اللي تطبيق اليوزر يبعتها:

```json
{ "action": "ConnectClient" }

{ "action": "GetOnlineDrivers" }

{ "action": "GetDriverLocation", "driverId": 5 }
```

### Events اللي السيرفر بيبعتها للكل:

| Action               | متى تتبعت                                  |
|----------------------|--------------------------------------------|
| `DriverConnected`    | سواق اتصل                                  |
| `DriverDisconnected` | سواق قفل الاتصال                           |
| `LocationUpdate`     | سواق حدّث موقعه                            |
| `DriverRemoved`      | سواق اتشال                                 |
| `OnlineDrivers`      | رد على `GetOnlineDrivers`/`ConnectClient`  |
| `DriverLocation`     | رد على `GetDriverLocation`                 |
| `Pong`               | رد على `Ping`                              |
| `Error`              | لو حصلت مشكلة                              |

> الكود: [WebSocketMiddleware.cs](Snap.APIs/Middlewares/WebSocketMiddleware.cs).

---

## 6) ملخص: مين بيعمل إيه؟

### تطبيق اليوزر (User App)

| الفعل                   | الـ Endpoint                                     |
|-------------------------|--------------------------------------------------|
| إنشاء اوردر             | `POST /api/Orders`                               |
| متابعة حالة الاوردر     | `GET /api/Orders/{id}` كل فترة                  |
| إلغاء الاوردر           | `PUT /api/Orders/driver` مع `status="cancelled"` |
| سجل الرحلات             | `GET /api/UserHistory/user/{userId}`             |
| تتبع موقع السواق        | WebSocket: `ConnectClient` + `GetDriverLocation` |

### تطبيق السواق (Driver App)

| الفعل                          | الـ Endpoint                                       |
|--------------------------------|----------------------------------------------------|
| جلب الاوردرات المتاحة          | `GET /api/Orders` + فلترة `status=="pending"`     |
| قبول اوردر                     | `PUT /api/Orders/driver` مع `status="accepted"`   |
| إنهاء الرحلة                   | `PUT /api/Orders/driver` مع `status="completed"`  |
| إلغاء الاوردر بعد القبول        | `PUT /api/Orders/driver` مع `status="cancelled"`  |
| تسجيل الرحلة                   | `POST /api/TripsHistory`                           |
| سجل الرحلات                    | `GET /api/TripsHistory/driver/{userId}`           |
| إرسال الموقع المباشر           | WebSocket: `ConnectDriver` + `UpdateLocation`     |

---

## 7) نقاط مهمة للـ Flutter Dev

1. **Default status** عند الإنشاء = `pending` — مش محتاج تبعته في الـ body.
2. **الإلغاء التلقائي** بعد ٤ دقايق pending — لازم اليوزر يتابع الاوردر بـ polling.
3. **DELETE بيمسح من الـ DB** خالص — استخدم `PUT /api/Orders/driver` لو عايز تخلي الاوردر cancelled مع الاحتفاظ بيه.
4. **مفيش filter on server** — كل الفلترة في الموبايل حاليًا.
5. **القيم النصية** خلي عينك على spelling: `pending`, `accepted`, `completed`, `cancelled` (lowercase دايمًا).
6. **`Type`** بيتحوّل lowercase تلقائيًا في السيرفر — `Ride` بيتخزن `ride`.
