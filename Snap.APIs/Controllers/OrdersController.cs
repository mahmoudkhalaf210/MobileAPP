using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Snap.APIs.DTOs;
using Snap.APIs.Errors;
using Snap.Core.Entities;
using Snap.Core.Services;
using Snap.Repository.Data;
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

        public OrdersController(SnapDbContext context, INotificationService notificationService, UserManager<User> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _userManager = userManager;
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

                // Notify all drivers
                try
                {
                    var drivers = await _userManager.GetUsersInRoleAsync("driver");
                    var driverIds = drivers.Select(d => d.Id).ToList();
                    var tokens = await _context.FCMTokenUsers
                        .Where(t => driverIds.Contains(t.UserId))
                        .Select(t => t.Token)
                        .ToListAsync();

                    foreach (var token in tokens)
                    {
                        if (!string.IsNullOrEmpty(token))
                            await _notificationService.SendNotification(token, "New Order Available", "Check the app for a new trip request!");
                    }
                }
                catch (Exception)
                {
                    // Continue even if notification fails
                }

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

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating order: {ex.Message}"));
            }
        }

        // PUT: api/Orders/driver
        [HttpPut("driver")]
        public async Task<IActionResult> UpdateOrderDriver([FromBody] UpdateOrderDriverDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Driverid = dto.Driverid;
                order.Status = dto.Status;
                await _context.SaveChangesAsync();

                if (order.Status == OrderStatus.Approved.GetStringValue())
                {
                    var userToken = await _context.FCMTokenUsers
                        .Where(t => t.UserId == order.UserId)
                        .Select(t => t.Token)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(userToken))
                    {
                        await _notificationService.SendNotification(userToken, "Order Approved", "A driver has accepted your order.");
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order driver: {ex.Message}"));
            }
        }

        // GET: api/Orders
        [HttpGet]
        public async Task<ActionResult<List<OrderDto>>> GetAllOrders()
        {
            try
            {
                var orders = await _context.Orders
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
                        PinkMode = o.PinkMode,
                        FCMToken = o.FCMToken
                    })
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting all orders: {ex.Message}"));
            }
        }

        // GET: api/Orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrderById(int id)
        {
            try
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
                    PinkMode = order.PinkMode,
                    FCMToken = order.FCMToken
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting order: {ex.Message}"));
            }
        }

        // POST: api/Orders/pending
        [HttpPost("pending")]
        public async Task<IActionResult> SetOrderPending([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Status = OrderStatus.Pending.GetStringValue();
                order.Driverid = dto.Driverid;
                await _context.SaveChangesAsync();

                var response = new UpdateOrderStatusResponseDto
                {
                    OrderId = order.Id,
                    FCMToken = order.FCMToken
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order status: {ex.Message}"));
            }
        }

        // POST: api/Orders/approve
        [HttpPost("approve")]
        public async Task<IActionResult> SetOrderApproved([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Status = OrderStatus.Approved.GetStringValue();
                order.Driverid = dto.Driverid;
                await _context.SaveChangesAsync();

                await _notificationService.SendNotification(dto.FCMToken ?? order.FCMToken, "Order Approved", "A driver has accepted your order.");

                var response = new UpdateOrderStatusResponseDto
                {
                    OrderId = order.Id,
                    FCMToken = order.FCMToken
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order status: {ex.Message}"));
            }
        }

        // POST: api/Orders/cancel
        [HttpPost("cancel")]
        public async Task<IActionResult> SetOrderCancel([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Status = OrderStatus.Cancel.GetStringValue();
                order.Driverid = dto.Driverid;
                await _context.SaveChangesAsync();

                var response = new UpdateOrderStatusResponseDto
                {
                    OrderId = order.Id,
                    FCMToken = order.FCMToken
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order status: {ex.Message}"));
            }
        }

        // POST: api/Orders/arrived
        [HttpPost("arrived")]
        public async Task<IActionResult> SetOrderArrived([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Status = OrderStatus.Arrived.GetStringValue();
                order.Driverid = dto.Driverid;
                await _context.SaveChangesAsync();

                var userToken = await _context.FCMTokenUsers
                    .Where(t => t.UserId == order.UserId)
                    .Select(t => t.Token)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(userToken))
                {
                    await _notificationService.SendNotification(userToken, "Driver Arrived", "Your driver has arrived.");
                }

                var response = new UpdateOrderStatusResponseDto
                {
                    OrderId = order.Id,
                    FCMToken = order.FCMToken
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order status: {ex.Message}"));
            }
        }

        // POST: api/Orders/started
        [HttpPost("started")]
        public async Task<IActionResult> SetOrderStarted([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Status = OrderStatus.Started.GetStringValue();
                order.Driverid = dto.Driverid;
                await _context.SaveChangesAsync();

                await _notificationService.SendNotification(dto.FCMToken ?? order.FCMToken, "Trip Started", "Your trip has started.");

                var response = new UpdateOrderStatusResponseDto
                {
                    OrderId = order.Id,
                    FCMToken = order.FCMToken
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order status: {ex.Message}"));
            }
        }

        // POST: api/Orders/complete
        [HttpPost("complete")]
        public async Task<IActionResult> SetOrderComplete([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _context.Orders.FindAsync(dto.OrderId);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                order.Status = OrderStatus.Complete.GetStringValue();
                order.Driverid = dto.Driverid;
                await _context.SaveChangesAsync();

                var userToken = await _context.FCMTokenUsers
                    .Where(t => t.UserId == order.UserId)
                    .Select(t => t.Token)
                    .FirstOrDefaultAsync();

                var driverToken = string.Empty;
                if (order.Driverid.HasValue)
                {
                    var driver = await _context.Drivers.FindAsync(order.Driverid.Value);
                    if (driver != null)
                    {
                        driverToken = await _context.FCMTokenUsers
                            .Where(t => t.UserId == driver.UserId)
                            .Select(t => t.Token)
                            .FirstOrDefaultAsync();
                    }
                }

                if (!string.IsNullOrEmpty(userToken))
                    await _notificationService.SendNotification(userToken, "Trip Completed", "Your trip has been completed.");

                if (!string.IsNullOrEmpty(driverToken))
                    await _notificationService.SendNotification(driverToken, "Trip Completed", "The trip has been completed.");

                var response = new UpdateOrderStatusResponseDto
                {
                    OrderId = order.Id,
                    FCMToken = order.FCMToken
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating order status: {ex.Message}"));
            }
        }

        // DELETE: api/Orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                    return NotFound(new ApiResponse(404, "Order not found"));

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while deleting order: {ex.Message}"));
            }
        }
    }
}