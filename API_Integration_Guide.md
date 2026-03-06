# API Integration Guide for Flutter UI

This document outlines the recent changes to the API to support role-based notifications and FCM token management.

## 1. FCM Token Management

### Save FCM Token
**Endpoint**: `POST /api/UsersIdentity/save-fcm-token`
**Auth**: None
**Description**: Call this endpoint after the user logs in and whenever the FCM token is refreshed on the device. This associates the device token with the current user identified by their email.

**Request Body**:
```json
{
  "token": "your_fcm_token_string",
  "email": "user@example.com"
}
```

**Response**:
```json
{
  "message": "FCM Token saved successfully."
}
```

## 2. User Registration

**Endpoint**: `POST /api/UsersIdentity/Register`
**Description**: Ensure the `UserType` field is correctly set to either `driver` or `passenger`. The backend now automatically assigns the corresponding role to the user upon registration.

**Request Body** (Standard RegisterDto):
```json
{
  "email": "user@example.com",
  "fullName": "John Doe",
  "phoneNumber": "01234567890",
  "password": "Password123!",
  "userType": "driver", // or "passenger"
  "gender": "male"
}
```

## 3. Order Notifications

The backend now handles notifications automatically based on the order lifecycle.

### New Order Created
*   **Trigger**: `POST /api/Orders`
*   **Recipients**: All users with the `driver` role.
*   **Notification**:
    *   Title: "New Order Available"
    *   Body: "Check the app for a new trip request!"

### Driver Arrived
*   **Trigger**: `POST /api/Orders/arrived`
*   **Recipients**: The user (passenger) who created the order.
*   **Mechanism**: The backend looks up the passenger's FCM token from the `FCMTokenUsers` table.
*   **Notification**:
    *   Title: "Driver Arrived"
    *   Body: "Your driver has arrived."

### Trip Completed
*   **Trigger**: `POST /api/Orders/complete`
*   **Recipients**: Both the Passenger and the Driver.
*   **Mechanism**: The backend looks up both tokens from the `FCMTokenUsers` table.
*   **Notification**:
    *   Title: "Trip Completed"
    *   Body: "Your trip has been completed." (Passenger) / "The trip has been completed." (Driver)

## 4. Test Notification

**Endpoint**: `POST /api/Orders/test-notification`
**Description**: Use this to test if a specific token receives notifications.

**Request Body**:
```json
{
  "token": "target_fcm_token",
  "title": "Optional Title",
  "body": "Optional Body"
}
```

## 5. DTO Changes

### FCMTokenDto
Used in `save-fcm-token` endpoint.
```csharp
public class FCMTokenDto
{
    [Required]
    public string Token { get; set; }
    [Required]
    public string Email { get; set; }
}
```
