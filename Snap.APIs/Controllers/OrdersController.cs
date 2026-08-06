using Microsoft.AspNetCore.Mvc;
using Snap.APIs.DTOs;
using Snap.APIs.Errors;
using Snap.APIs.Services;
using Snap.Core.Entities;
using Snap.Core.Services;

namespace Snap.APIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService        _orderService;
        private readonly INotificationService _notificationService;

        public OrdersController(IOrderService orderService, INotificationService notificationService)
        {
            _orderService        = orderService;
            _notificationService = notificationService;
        }

        // POST: api/orders/test-notification
        [HttpPost("test-notification")]
        public async Task<IActionResult> TestNotification([FromBody] TestNotificationDto dto)
        {
            if (string.IsNullOrEmpty(dto.Token))
                return BadRequest("Token is required");

            await _notificationService.SendNotification(
                dto.Token,
                dto.Title ?? "Test Notification",
                dto.Body  ?? "This is a test notification from backend",
                new Dictionary<string, string>
                {
                    { "type",    "test"             },
                    { "message", dto.Body ?? "Test" }
                });

            return Ok("Notification sent");
        }

        // POST: api/orders
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                // OrderService persists the order AND enqueues driver notifications
                // as a background job before returning. No fire-and-forget here.
                var orderDto = await _orderService.CreateOrderAsync(dto);
                return Ok(orderDto);
            }
            catch (ArgumentException ex)    { return BadRequest(new ApiResponse(400, ex.Message)); }
            catch (KeyNotFoundException ex) { return NotFound(new ApiResponse(404, ex.Message)); }
            catch (Exception ex)            { return StatusCode(500, new ApiResponse(500, ex.Message)); }
        }

        // PUT: api/orders/driver  — Accept | Arrive | Start | Complete | Cancel
        [HttpPut("driver")]
        public async Task<IActionResult> UpdateOrderDriver([FromBody] UpdateOrderDriverDto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                if (string.Equals(dto.Status, "scheduled_accepted", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dto.Status, "accepted_scheduled", StringComparison.OrdinalIgnoreCase))
                {
                    await _orderService.AcceptScheduledOrderAsync(dto);
                    return Ok(new ApiResponse(200, "Scheduled order accepted successfully"));
                }

                var targetStatus = OrderStatusExtensions.FromString(dto.Status);

                if (targetStatus == OrderStatus.Approved)
                    await _orderService.AcceptOrderAsync(dto);
                else if (targetStatus == OrderStatus.Cancel)
                    await _orderService.CancelOrderByDriverAsync(dto);
                else
                    await _orderService.HandleWorkflowTransitionAsync(dto, targetStatus);

                return Ok(new ApiResponse(200, "Order updated successfully"));
            }
            catch (KeyNotFoundException ex)      { return NotFound(new ApiResponse(404, ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(new ApiResponse(400, ex.Message)); }
            catch (Exception ex)                 { return StatusCode(500, new ApiResponse(500, ex.Message)); }
        }

        // PUT: api/orders/user/cancel
        [HttpPut("user/cancel")]
        public async Task<IActionResult> CancelOrderByUser([FromBody] CancelOrderByUserDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                await _orderService.CancelOrderByUserAsync(dto);
                return Ok(new ApiResponse(200, "Order cancelled"));
            }
            catch (KeyNotFoundException ex)       { return NotFound(new ApiResponse(404, ex.Message)); }
            catch (UnauthorizedAccessException ex) { return BadRequest(new ApiResponse(403, ex.Message)); }
            catch (InvalidOperationException ex)   { return BadRequest(new ApiResponse(400, ex.Message)); }
            catch (Exception ex)                   { return StatusCode(500, new ApiResponse(500, ex.Message)); }
        }

        // GET: api/orders
        [HttpGet]
        public async Task<ActionResult<List<OrderDto>>> GetAllOrders()
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(orders);
        }

        // GET: api/orders/user/{userId}/scheduled
        [HttpGet("user/{userId}/scheduled")]
        public async Task<ActionResult<List<OrderDto>>> GetScheduledOrdersForUser(string userId)
        {
            try
            {
                var orders = await _orderService.GetScheduledOrdersByUserAsync(userId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        // GET: api/orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrderById(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                return Ok(order);
            }
            catch (KeyNotFoundException ex) { return NotFound(new ApiResponse(404, ex.Message)); }
        }

        // DELETE: api/orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            try
            {
                await _orderService.DeleteOrderAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new ApiResponse(404, ex.Message)); }
        }
    }
}
