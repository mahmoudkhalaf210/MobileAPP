# API Testing Guide

This guide explains how to test the Snap&Shape API using the provided tools.

## Prerequisites
1. **Running Backend**: Ensure the API is running (`dotnet run --project Snap.APIs`).
2. **Database Data**: You need at least one valid **User** and one valid **Driver** in your database.
   - Update the scripts with their IDs.

## Option 1: Automated PowerShell Script
Use `Test-OrderCycle.ps1` to run a full happy-path test automatically.

1. Open `Test-OrderCycle.ps1` in a text editor.
2. Update `$userId` and `$driverId` with valid IDs from your database.
3. Update `$baseUrl` if your API is not running on port 5200.
4. Run the script in PowerShell:
   ```powershell
   .\Test-OrderCycle.ps1
   ```

## Option 2: VS Code REST Client (.http)
Use `ApiTests.http` for manual, step-by-step testing inside VS Code.

1. Install the "REST Client" extension for VS Code.
2. Open `ApiTests.http`.
3. Update `@userId` and `@driverId` at the top of the file.
4. Click **Send Request** above each step in order:
   - Step 1: Make Driver Online
   - Step 2: Create Order (captures `orderId` automatically)
   - Step 3: Accept Order
   - Step 4: Arrive
   - Step 5: Start
   - Step 6: Complete

## API Order Cycle
The typical flow tested is:
1. **Driver Online**: Driver updates location to be near the pickup point.
2. **Create Order**: User requests a ride (`Status: pending`).
3. **Approve**: Driver accepts the ride (`Status: approve`).
4. **Arrived**: Driver reaches pickup location (`Status: Arrived`).
5. **Started**: Trip begins (`Status: Started`).
6. **Complete**: Trip ends (`Status: Complete`).
