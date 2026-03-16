# Test-OrderCycle.ps1
# Automated Test Script for Snap&Shape Order Lifecycle
# Usage: .\Test-OrderCycle.ps1

# ---------------------------------------------------------
# Configuration
# ---------------------------------------------------------
$baseUrl = "https://localhost:7155" # Updated port to match launchSettings (https)
$driverId = 1                       # Must exist in Drivers table
$userId = "6c1369a2-6cf1-4ba4-a6a2-c53bda4362e7" # User provided ID
$driverName = "Test Driver"
$fcmToken = "fewzqcfvAEPQvhfULpCjij:APA91bGv8rNIyB-nGkPp4fwzqbahUIGfI6MeK19gnt02O-Jd6Nty02DkTJNWeRP4MhFrVs8rzV__gKmvSxzZ2JqHByRpxGIBJZLqzP28UZIDmXwGCLNb5lc"

# ---------------------------------------------------------
# SSL Bypass for Localhost (Self-Signed Certs)
# ---------------------------------------------------------
if ([System.Net.ServicePointManager]::ServerCertificateValidationCallback -eq $null) {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
}

# ---------------------------------------------------------
# Helper function to invoke REST API
# ---------------------------------------------------------
function Invoke-SnapApi {
    param (
        [string]$Method,
        [string]$Uri,
        [object]$Body
    )

    $url = "$baseUrl$Uri"
    Write-Host "[$Method] $url" -ForegroundColor Cyan
    
    try {
        if ($Body) {
            $jsonBody = $Body | ConvertTo-Json -Depth 5
            $response = Invoke-RestMethod -Method $Method -Uri $url -Body $jsonBody -ContentType "application/json" -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Method $Method -Uri $url -ContentType "application/json" -ErrorAction Stop
        }
        
        Write-Host "Success" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Response Body: $errorBody" -ForegroundColor Yellow
        }
        return $null
    }
}

# ---------------------------------------------------------
# Step 1: Make Driver Online (Update Location)
# ---------------------------------------------------------
Write-Host "`nStep 1: Making Driver Online..." -ForegroundColor Magenta
$locationBody = @{
    DriverId = $driverId
    Lat = 30.0444
    Lng = 31.2357
    Timestamp = (Get-Date).ToString("o")
}
$locationResponse = Invoke-SnapApi -Method POST -Uri "/api/Location/update" -Body $locationBody

if (-not $locationResponse) {
    Write-Host "Failed to update driver location. Ensure driver exists." -ForegroundColor Red
    exit
}

# ---------------------------------------------------------
# Step 2: Create Order
# ---------------------------------------------------------
Write-Host "`nStep 2: Creating Order..." -ForegroundColor Magenta
$orderBody = @{
    UserId = $userId
    Date = (Get-Date).AddMinutes(10).ToString("o")
    From = "Cairo Tower"
    To = "Giza Pyramids"
    FromLatLng = @{
        Lat = 30.0444
        Lng = 31.2357
    }
    ToLatLng = @{
        Lat = 29.9792
        Lng = 31.1342
    }
    ExpectedPrice = 50.0
    Type = "Standard"
    Distance = 12.5
    Notes = "Test order from script"
    NoPassengers = 2
    PaymentWay = "Cash"
    CarType = "Sedan"
    PinkMode = $false
    FCMToken = $fcmToken
}
$orderResponse = Invoke-SnapApi -Method POST -Uri "/api/Orders" -Body $orderBody

if (-not $orderResponse) {
    Write-Host "Failed to create order. Ensure user exists." -ForegroundColor Red
    exit
}

$orderId = $orderResponse.id
Write-Host "Order Created! ID: $orderId" -ForegroundColor Green

# ---------------------------------------------------------
# Step 3: Driver Accepts Order
# ---------------------------------------------------------
Write-Host "`nStep 3: Driver Accepting Order..." -ForegroundColor Magenta
$acceptBody = @{
    OrderId = $orderId
    Driverid = $driverId
    Status = "approve"
    FCMToken = "driver-fcm-token-placeholder"
}
$acceptResponse = Invoke-SnapApi -Method PUT -Uri "/api/Orders/driver" -Body $acceptBody

# ---------------------------------------------------------
# Step 4: Driver Arrives
# ---------------------------------------------------------
Write-Host "`nStep 4: Driver Arrived..." -ForegroundColor Magenta
Start-Sleep -Seconds 2
$arriveBody = @{
    OrderId = $orderId
    Driverid = $driverId
    Status = "arrived"
}
$arriveResponse = Invoke-SnapApi -Method PUT -Uri "/api/Orders/driver" -Body $arriveBody

# ---------------------------------------------------------
# Step 5: Start Trip
# ---------------------------------------------------------
Write-Host "`nStep 5: Trip Started..." -ForegroundColor Magenta
Start-Sleep -Seconds 2
$startBody = @{
    OrderId = $orderId
    Driverid = $driverId
    Status = "started"
}
$startResponse = Invoke-SnapApi -Method PUT -Uri "/api/Orders/driver" -Body $startBody

# ---------------------------------------------------------
# Step 6: Complete Trip
# ---------------------------------------------------------
Write-Host "`nStep 6: Trip Completed..." -ForegroundColor Magenta
Start-Sleep -Seconds 2
$completeBody = @{
    OrderId = $orderId
    Driverid = $driverId
    Status = "complete"
}
$completeResponse = Invoke-SnapApi -Method PUT -Uri "/api/Orders/driver" -Body $completeBody

Write-Host "`nTest Cycle Completed Successfully!" -ForegroundColor Cyan
