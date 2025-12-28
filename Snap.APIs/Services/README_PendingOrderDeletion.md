# Pending Order Deletion Service

## Overview
This service implements an **outbox pattern** approach to automatically delete expired pending orders. It runs as a background service and checks every minute for pending orders that are older than 4 minutes, then deletes them.

## How It Works

1. **Background Service**: Runs continuously while the application is running
2. **Periodic Check**: Executes every 1 minute
3. **Expiration Logic**: Finds all pending orders where `Date < (CurrentTime - 4 minutes)`
4. **Bulk Deletion**: Uses EF Core's `ExecuteDeleteAsync` for efficient bulk deletion
5. **Logging**: Comprehensive logging for monitoring and debugging

## Features

✅ **Clean Code Architecture**
- Single Responsibility Principle
- Proper error handling
- Comprehensive logging
- Dependency injection
- Cancellation token support

✅ **Performance Optimized**
- Uses `ExecuteDeleteAsync` for bulk operations (no entity tracking overhead)
- Efficient query with proper filtering
- Minimal database round trips

✅ **Reliable**
- Exception handling with proper logging
- Graceful shutdown support
- Transaction-safe operations

## Configuration

The service uses the following constants (can be modified in the code):

- `ExpirationMinutes = 4`: Orders older than this will be deleted
- `CheckIntervalMinutes = 1`: How often to check for expired orders

## Registration

The service is automatically registered in `Program.cs`:

```csharp
builder.Services.AddHostedService<Snap.APIs.Services.PendingOrderDeletionService>();
```

## Logging

The service logs the following events:
- Service start/stop
- Number of expired orders found
- Successful deletions with order IDs
- Errors and exceptions

## Does This Fix the Problem?

**YES!** This solution will:

1. ✅ **Automatically delete pending orders** after 4 minutes
2. ✅ **Run continuously** while the application is running
3. ✅ **No manual intervention** required
4. ✅ **No SQL Server Agent** needed
5. ✅ **Clean, maintainable code**

### Important Notes:

⚠️ **Application Must Be Running**: This service only works when your application is running. If the application stops, the service stops too.

⚠️ **Timing**: Orders are checked every minute, so an order might be deleted anywhere between 4-5 minutes after creation (depending on when the check runs).

### Alternative Solutions:

If you need deletion to work even when the application is offline, consider:
- **SQL Server Service Broker** (see `Database/ServiceBroker_AutoDeletePendingOrders.sql`)
- **SQL Server Agent Job** (requires SQL Server Agent to be running)
- **Database Trigger with Timer** (complex, but works at DB level)

## Testing

To test the service:

1. Create a pending order with a date 5 minutes in the past
2. Wait for the next check cycle (up to 1 minute)
3. Verify the order is deleted
4. Check application logs for confirmation

## Monitoring

Monitor the service through:
- Application logs (check for "PendingOrderDeletionService" entries)
- Database queries to verify orders are being deleted
- Application performance metrics



