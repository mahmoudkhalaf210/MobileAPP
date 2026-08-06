# Snap v2 Endpoints

## CarType enum (sent/received as string)
`Car`, `Scooter`, `SuperMalaky`, `Taxi`

## REST

- `POST /api/v2/orders/normal` — creates an immediate (non-scheduled) order, with typed `CarType`.
- `POST /api/v2/orders/schedule` — creates a scheduled order (requires future `Date`), with typed `CarType`.
- `POST /api/v2/explore` — creates an explore place (name, description, lat/lng, photo).
- `GET /api/v2/explore` — lists all explore places.
- `GET /api/v2/explore/{id}` — gets one explore place.
- `PUT /api/v2/explore/{id}` — updates an explore place.
- `DELETE /api/v2/explore/{id}` — deletes an explore place.
- `GET /api/v2/points/{userId}` — gets a user's points balance.

## SignalR hub — `/hubs/v2/orders`

Client-invoked methods:
- `Register(userId)` — joins the connection to that user's real-time group; call once after connecting.
- `GetAllOrders()` — returns all active (non-cancelled) orders.
- `GetScheduledOrdersForUser(userId)` — returns the user's scheduled orders.
- `GetActiveOrders(userId)` — returns the user's active orders (Activity Screen).
- `GetCompletedOrders(userId)` — returns the user's completed orders (Activity Screen).
- `GetCancelledOrders(userId)` — returns the user's cancelled orders (Activity Screen).

Server-push events (listen, then re-call the matching method above to refresh):
- `AllOrdersUpdated` — the global order list changed.
- `ScheduledOrdersUpdated` — the user's scheduled orders changed.
- `ActiveOrdersUpdated` — the user's active orders changed.
- `CompletedOrdersUpdated` — the user's completed orders changed (points were also awarded).
- `CancelledOrdersUpdated` — the user's cancelled orders changed.

## Points

- Awarded automatically when an order completes (no endpoint call needed to earn them).
- Amount per completed order is configurable server-side (`PointsSettings.PointsPerCompletedOrder`, default 10).
