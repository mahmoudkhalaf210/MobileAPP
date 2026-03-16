# Flutter API Integration Guide

This document outlines the API changes and endpoints required for the Flutter UI to integrate with the backend order management and driver location systems.

## 1. Driver Location Management

### Send Location Updates (Driver App)
The driver app **MUST** send periodic location updates to be visible for new orders.

*   **Endpoint:** `POST /api/location/update`
*   **Headers:** `Content-Type: application/json`
*   **Request Body:**
    ```json
    {
      "driverId": 123,
      "lat": 30.0444,
      "lng": 31.2357,
      "timestamp": "2023-10-27T10:00:00Z"
    }
    ```
*   **Response:** `200 OK`

## 2. Order Management Cycle

### A. Create Order (User App)
Creates a new order and notifies nearby drivers (within 10km) via FCM.

*   **Endpoint:** `POST /api/Orders`
*   **Request Body:**
    ```json
    {
      "userId": "user_guid_here",
      "date": "2023-10-27T12:00:00Z",
      "from": "Cairo",
      "to": "Giza",
      "fromLatLng": { "lat": 30.0, "lng": 31.0 },
      "toLatLng": { "lat": 30.1, "lng": 31.1 },
      "expectedPrice": 50.0,
      "type": "ride",
      "distance": 5.5,
      "notes": "Waiting at gate",
      "noPassengers": 1,
      "paymentWay": "cash",
      "carType": "sedan",
      "pinkMode": false,
      "fcmToken": "user_fcm_token_here"
    }
    ```
*   **Response:** `200 OK` with created Order object.

### B. Driver Actions (Driver App)
This single endpoint handles all driver actions: Accept, Arrive, Start Trip, Complete Trip, Cancel.

*   **Endpoint:** `PUT /api/Orders/driver`
*   **Headers:** `Content-Type: application/json`

#### 1. Accept Order (Approve)
*   **Action:** Driver accepts a pending order.
*   **Status:** `approve`
*   **Request Body:**
    ```json
    {
      "orderId": 101,
      "driverId": 123,
      "status": "approve"
    }
    ```
*   **Note:** This uses **Atomic Locking**. If another driver accepted it milliseconds ago, you will receive `400 Bad Request`. Handle this in UI (e.g., "Order already taken").

#### 2. Driver Arrived
*   **Action:** Driver reached pickup location.
*   **Status:** `Arrived`
*   **Request Body:**
    ```json
    {
      "orderId": 101,
      "driverId": 123,
      "status": "Arrived"
    }
    ```
*   **Notification:** Sends "Driver Arrived" FCM to User.

#### 3. Start Trip
*   **Action:** Trip has begun.
*   **Status:** `Started`
*   **Request Body:**
    ```json
    {
      "orderId": 101,
      "driverId": 123,
      "status": "Started"
    }
    ```
*   **Notification:** Sends "Trip Started" FCM to User.

#### 4. Complete Trip
*   **Action:** Trip finished.
*   **Status:** `Complete`
*   **Request Body:**
    ```json
    {
      "orderId": 101,
      "driverId": 123,
      "status": "Complete"
    }
    ```
*   **Notification:** Sends "Trip Completed" FCM to User.

#### 5. Cancel Order
*   **Action:** Driver cancels the order.
*   **Status:** `cancelled`
*   **Request Body:**
    ```json
    {
      "orderId": 101,
      "driverId": 123,
      "status": "cancelled"
    }
    ```
*   **Notification:** Sends "Order Cancelled" FCM to User.

## 3. Order Status Flow
The allowed transitions are strictly enforced:
1.  `pending` -> `approve` (Accept)
2.  `approve` -> `Arrived`
3.  `Arrived` -> `Started`
4.  `Started` -> `Complete`
*   `cancelled` can happen from any state (implementation specific, currently mostly from Pending/Approved).

## 4. FCM Notifications
Ensure the Flutter app subscribes/listens to these FCM messages:

| Title | Body | Trigger |
| :--- | :--- | :--- |
| **New Order Available** | "Check the app for a new trip request!" | New Order created nearby |
| **Order Accepted** | "{DriverName} has accepted your order!" | Driver accepts order |
| **Driver Arrived** | "Your driver has arrived..." | Driver updates status to Arrived |
| **Trip Started** | "Your trip has started..." | Driver updates status to Started |
| **Trip Completed** | "You have arrived..." | Driver updates status to Complete |
| **Order Cancelled** | "Your order has been cancelled." | Driver cancels or Timeout (4 mins) |

## 5. Automated Cancellation
*   Orders remaining in `pending` status for more than **4 minutes** are automatically cancelled by the server.
*   Users will receive an FCM notification: "We could not find a driver for your order at this time."
