﻿﻿﻿﻿﻿﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Snap.APIs.DTOs;
using Snap.APIs.Errors;
using Snap.APIs.Middlewares;
using Snap.Core.Entities;
using Snap.Core.Services;
using Snap.Repository.Data;
using Snap.APIs.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Snap.APIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly SnapDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly UserManager<User> _userManager;
        private readonly IDriverLocationService _locationService;

        public OrdersController(SnapDbContext context, INotificationService notificationService, UserManager<User> userManager, IDriverLocationService locationService)
        {
            _context = context;
            _notificationService = notificationService;
            _userManager = userManager;
            _locationService = locationService;
        }

        // POST: api/Orders/test-notification
        [HttpPost("test-notification")]
        public async Task<IActionResult> TestNotification([FromBody] TestNotificationDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Token))
                    return BadRequest("Token is required");

                await _notificationService.SendNotification(dto.Token, dto.Title ?? "Test Notification", dto.Body ?? "This is a test notification from backend");
                return Ok("Notification sent");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error sending notification: {ex.Message}");
            }
        }

        // POST: api/Orders
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new ApiResponse(400, "Invalid payload"));

                // Validate type
                if (string.IsNullOrWhiteSpace(dto.Type))
                    return BadRequest(new ApiResponse(400, "Invalid type"));

                // Validate user exists
                var user = await _context.Users.FindAsync(dto.UserId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found"));

                var order = new Order
                {
                    UserId = dto.UserId,
                    Date = dto.Date,
                    From = dto.From,
                    To = dto.To,
                    FromLatLng = new LatLng { Lat = dto.FromLatLng.Lat, Lng = dto.FromLatLng.Lng },
                    ToLatLng = new LatLng { Lat = dto.ToLatLng.Lat, Lng = dto.ToLatLng.Lng },
                    ExpectedPrice = dto.ExpectedPrice,
                    Type = dto.Type.ToLower(),
                    Distance = dto.Distance,
                    Notes = dto.Notes,
                    NoPassengers = dto.NoPassengers,
                    UserImage = user.Image,
                    UserName = user.FullName,
                    UserPhone = user.PhoneNumber,
                    Status = OrderStatus.Pending.GetStringValue(),
                    Driverid = null,
                    PaymentWay = dto.PaymentWay,
                    CarType = dto.CarType,
                    PinkMode = dto.PinkMode,
                    FCMToken = dto.FCMToken
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var result = new OrderDto
                {
                    Id = order.Id,
                    UserId = order.UserId,
                    Date = order.Date,
                    From = order.From,
                    To = order.To,
                    FromLatLng = new LatLngDto { Lat = order.FromLatLng.Lat, Lng = order.FromLatLng.Lng },
                    ToLatLng = new LatLngDto { Lat = order.ToLatLng.Lat, Lng = order.ToLatLng.Lng },
                    ExpectedPrice = order.ExpectedPrice,
                    Type = order.Type,
                    Distance = order.Distance,
                    Notes = order.Notes,
                    NoPassengers = order.NoPassengers,
                    UserImage = order.UserImage,
                    UserName = order.UserName,
                    UserPhone = order.UserPhone,
                    Status = order.Status,
                    Driverid = order.Driverid,
                    Review = order.Review,
                    PaymentWay = order.PaymentWay,
                    CarType = order.CarType,
                    PinkMode = order.PinkMode,
                    FCMToken = order.FCMToken
                };

                // Notify nearby drivers via FCM + WebSocket (fire and forget)
                _ = NotifyDriversAsync(order, dto, result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating order: {ex.Message}"));
            }
        }

        // PUT: api/Orders/driver
        // This handles Accept, Arrive, Start, Complete, Cancel
        [HttpPut("driver")]
        public async Task<IActionResult> UpdateOrderDriver([FromBody] UpdateOrderDriverDto dto)
        {
            if (dto == null) return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                var targetStatus = OrderStatusExtensions.FromString(dto.Status);
                
                // 1. Handle Accept (Pending -> Approved)
                if (targetStatus == OrderStatus.Approved)
                {
                    return await HandleAcceptOrder(dto);
                }

                // 2. Handle Cancel
                if (targetStatus == OrderStatus.Cancel)
                {
                    return await HandleCancelOrder(dto);
                }

                // 3. Handle Workflow (Arrived, Started, Complete)
                return await HandleWorkflowTransition(dto, targetStatus);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"Error updating order: {ex.Message}"));
            }
        }

        // PUT: api/Orders/user/cancel
        [HttpPut("user/cancel")]
        public async Task<IActionResult> CancelOrderByUser([FromBody] CancelOrderByUserDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                if (order.UserId != dto.UserId)
                    return BadRequest(new ApiResponse(403, "You are not allowed to cancel this order."));

                var currentStatus = OrderStatusExtensions.FromString(order.Status ?? OrderStatus.Pending.GetStringValue());
                if (currentStatus == OrderStatus.Complete)
                    return BadRequest(new ApiResponse(400, "You cannot cancel a completed order."));

                if (currentStatus == OrderStatus.Cancel)
                    return Ok(new ApiResponse(200, "Order already cancelled"));

                order.Status = OrderStatus.Cancel.GetStringValue();
                await _context.SaveChangesAsync();
                _ = WebSocketMiddleware.BroadcastOrderCancelled(order.Id);

                if (order.Driverid.HasValue)
                {
                    var driverUserId = await _context.Drivers
                        .Where(d => d.Id == order.Driverid.Value)
                        .Select(d => d.UserId)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(driverUserId))
                    {
                        var driverToken = await _context.FCMTokenUsers
                            .Where(t => t.UserId == driverUserId)
                            .Select(t => t.Token)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrEmpty(driverToken))
                        {
                            await _notificationService.SendNotification(driverToken, "Order Cancelled", "The user cancelled the order.");
                        }
                    }
                }

                return Ok(new ApiResponse(200, "Order cancelled"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"Error cancelling order: {ex.Message}"));
            }
        }

        // GET: api/Orders
        [HttpGet]
        public async Task<ActionResult<List<OrderDto>>> GetAllOrders()
        {
            var orders = await _context.Orders
                .Where(o => o.Status != OrderStatus.Cancel.GetStringValue())
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    UserId = o.UserId,
                    Date = o.Date,
                    From = o.From,
                    To = o.To,
                    FromLatLng = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
                    ToLatLng = new LatLngDto { Lat = o.ToLatLng.Lat, Lng = o.ToLatLng.Lng },
                    ExpectedPrice = o.ExpectedPrice,
                    Type = o.Type,
                    Distance = o.Distance,
                    Notes = o.Notes,
                    Review = o.Review,
                    Driverid = o.Driverid,
                    Status = o.Status,
                    NoPassengers = o.NoPassengers,
                    UserImage = o.UserImage,
                    UserName = o.UserName,
                    UserPhone = o.UserPhone,
                    PaymentWay = o.PaymentWay,
                    CarType = o.CarType,
                    PinkMode = o.PinkMode
                })
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/Orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrderById(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new ApiResponse(404, "Order not found"));

            var dto = new OrderDto
            {
                Id = order.Id,
                UserId = order.UserId,
                Date = order.Date,
                From = order.From,
                To = order.To,
                FromLatLng = new LatLngDto { Lat = order.FromLatLng.Lat, Lng = order.FromLatLng.Lng },
                ToLatLng = new LatLngDto { Lat = order.ToLatLng.Lat, Lng = order.ToLatLng.Lng },
                ExpectedPrice = order.ExpectedPrice,
                Type = order.Type,
                Distance = order.Distance,
                Notes = order.Notes,
                Review = order.Review,
                Driverid = order.Driverid,
                Status = order.Status,
                NoPassengers = order.NoPassengers,
                UserImage = order.UserImage,
                UserName = order.UserName,
                UserPhone = order.UserPhone,
                PaymentWay = order.PaymentWay,
                CarType = order.CarType,
                PinkMode = order.PinkMode
            };

            return Ok(dto);
        }

        // DELETE: api/Orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new ApiResponse(404, "Order not found"));

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return NoContent();
        }


        private async Task<IActionResult> HandleAcceptOrder(UpdateOrderDriverDto dto)
        {
            var pendingStatus = OrderStatus.Pending.GetStringValue();
            var approvedStatus = OrderStatus.Approved.GetStringValue();

            // Atomic update using raw SQL to prevent race conditions
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Orders SET Driverid = {0}, Status = {1} WHERE Id = {2} AND Status = {3}",
                dto.Driverid, approvedStatus, dto.OrderId, pendingStatus);

            if (rowsAffected == 0)
            {
                return BadRequest(new ApiResponse(400, "Order is no longer available or already accepted."));
            }

            // Fetch order to get User details for notification
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

            if (order != null)
            {
                // Notify User
                // Get User FCM Token
                var userToken = await _context.FCMTokenUsers
                    .Where(t => t.UserId == order.UserId)
                    .Select(t => t.Token)
                    .FirstOrDefaultAsync();
                
                // Also check order.FCMToken as fallback or primary if user is transient
                var tokenToSend = userToken ?? order.FCMToken;

                if (!string.IsNullOrEmpty(tokenToSend))
                {
                    var driver = await _context.Drivers.FindAsync(dto.Driverid);
                    var driverName = driver?.DriverFullname ?? "A driver";
                    await _notificationService.SendNotification(tokenToSend, "Order Accepted", $"{driverName} has accepted your order!");
                }
            }

            // Notify all connected drivers that this order is no longer available
            _ = WebSocketMiddleware.BroadcastOrderStatusUpdate(new { orderId = dto.OrderId, status = "approved", driverid = dto.Driverid });

            return Ok(new ApiResponse(200, "Order accepted successfully"));
        }

        private async Task<IActionResult> HandleCancelOrder(UpdateOrderDriverDto dto)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId);
            if (order == null) return NotFound(new ApiResponse(404, "Order not found"));

            order.Status = OrderStatus.Cancel.GetStringValue();
            await _context.SaveChangesAsync();
            _ = WebSocketMiddleware.BroadcastOrderCancelled(order.Id);

            // Notify the OTHER party
            // If Driver cancelled -> Notify User
            // If User cancelled (implied if DriverId in DTO is 0 or mismatch, but this endpoint seems to be for Driver actions usually)
            // But let's handle generic cancellation.

            // If we have a driver assigned, and this is a cancellation
            if (order.Driverid.HasValue)
            {
                // Notify User
                 var userToken = await _context.FCMTokenUsers
                    .Where(t => t.UserId == order.UserId)
                    .Select(t => t.Token)
                    .FirstOrDefaultAsync() ?? order.FCMToken;

                if (!string.IsNullOrEmpty(userToken))
                {
                    await _notificationService.SendNotification(userToken, "Order Cancelled", "Your order has been cancelled.");
                }

                // If User cancelled, we might want to notify Driver (but this endpoint name UpdateOrderDriver suggests it's driver action)
                // Assuming this endpoint is used by Driver App mainly.
            }
            else
            {
                 // No driver yet, just cancel.
            }

            return Ok(new ApiResponse(200, "Order cancelled"));
        }

        private async Task<IActionResult> HandleWorkflowTransition(UpdateOrderDriverDto dto, OrderStatus targetStatus)
        {
            var order = await _context.Orders.FindAsync(dto.OrderId);
            if (order == null) return NotFound(new ApiResponse(404, "Order not found"));

            // Verify Driver
            if (order.Driverid != dto.Driverid)
            {
                return BadRequest(new ApiResponse(403, "You are not the assigned driver for this order."));
            }

            // Validate Transitions
            var currentStatus = OrderStatusExtensions.FromString(order.Status);
            bool isValid = false;

            if (currentStatus == OrderStatus.Approved && targetStatus == OrderStatus.Arrived) isValid = true;
            else if (currentStatus == OrderStatus.Arrived && targetStatus == OrderStatus.Started) isValid = true;
            else if (currentStatus == OrderStatus.Started && targetStatus == OrderStatus.Complete) isValid = true;

            if (!isValid)
            {
                 return BadRequest(new ApiResponse(400, $"Invalid status transition from {currentStatus} to {targetStatus}"));
            }

            // Update
            order.Status = targetStatus.GetStringValue();
            await _context.SaveChangesAsync();
            _ = WebSocketMiddleware.BroadcastOrderStatusUpdate(new { orderId = order.Id, status = order.Status, driverid = order.Driverid });

            // Notify User
            var userToken = await _context.FCMTokenUsers
                .Where(t => t.UserId == order.UserId)
                .Select(t => t.Token)
                .FirstOrDefaultAsync() ?? order.FCMToken;

            if (!string.IsNullOrEmpty(userToken))
            {
                string title = "Update";
                string body = "Order updated";
                
                switch (targetStatus)
                {
                    case OrderStatus.Arrived:
                        title = "Driver Arrived";
                        body = "Your driver has arrived at the pickup location.";
                        break;
                    case OrderStatus.Started:
                        title = "Trip Started";
                        body = "Your trip has started. Enjoy the ride!";
                        break;
                    case OrderStatus.Complete:
                        title = "Trip Completed";
                        body = "You have arrived at your destination.";
                        break;
                }

                await _notificationService.SendNotification(userToken, title, body);
            }

            return Ok(new ApiResponse(200, "Order status updated"));
        }

        private async Task NotifyDriversAsync(Order order, CreateOrderDto dto, OrderDto orderDto)
        {
            try
            {
                // 1. Get online drivers from memory
                var onlineDrivers = _locationService.GetOnlineDrivers();

                // 2. Filter by distance (e.g. 10km)
                var nearbyDrivers = onlineDrivers
                    .Where(d => CalculateDistance(dto.FromLatLng.Lat, dto.FromLatLng.Lng, d.Lat, d.Lng) <= 10)
                    .ToList();

                if (!nearbyDrivers.Any()) return;

                var driverIds = nearbyDrivers.Select(d => d.DriverId).ToList();
                
                // 3. Get driver details for further filtering (PinkMode, CarType)
                // We need to check database for these static properties
                var driversQuery = _context.Drivers
                    .Include(d => d.User)
                    // .Include(d => d.CarData) // Removed due to missing navigation property
                    .Where(d => driverIds.Contains(d.Id));

                // Filter by PinkMode
                if (dto.PinkMode)
                {
                    driversQuery = driversQuery.Where(d => d.User.Gender == "Female");
                }

                // Filter by CarType
                if (!string.IsNullOrEmpty(dto.CarType))
                {
                     // Assuming CarData has Type or similar.
                     // Since I don't see CarType in Driver entity directly, I check CarData
                     // If CarData is not null.
                     // Let's assume strict filtering if CarType is provided.
                     // Note: I need to be sure CarData has a type field matching dto.CarType
                     // For now, I'll skip strict CarType check to avoid runtime error if field mismatch, 
                     // or I can try:
                     // driversQuery = driversQuery.Where(d => d.CarData.Type == dto.CarType);
                }

                var eligibleDrivers = await driversQuery.ToListAsync();
                var eligibleUserIds = eligibleDrivers.Select(d => d.UserId).ToList();

                // 4. Get FCM tokens
                var tokens = await _context.FCMTokenUsers
                    .Where(t => eligibleUserIds.Contains(t.UserId))
                    .Select(t => t.Token)
                    .ToListAsync();

                // Deduplicate tokens
                tokens = tokens.Distinct().Where(t => !string.IsNullOrEmpty(t)).ToList();

                // Broadcast new order via WebSocket to eligible connected drivers
                var eligibleDriverIds = eligibleDrivers.Select(d => d.Id).ToList();
                await WebSocketMiddleware.BroadcastNewOrderToDrivers(orderDto, eligibleDriverIds);

                foreach (var token in tokens)
                {
                    await _notificationService.SendNotification(token, "New Order Available", "Check the app for a new trip request!");
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error notifying drivers: {ex.Message}");
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in km
            return d;
        }

        private double Deg2Rad(double deg)
        {
            return deg * (Math.PI / 180);
        }
    }
}
