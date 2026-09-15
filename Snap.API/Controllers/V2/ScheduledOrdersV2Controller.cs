using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Orders.Interfaces;

namespace Snap.API.Controllers.V2
{
    // Reads for scheduled orders only (Status "scheduled" / "scheduled_accepted").
    // Creation stays on POST api/v2/orders/schedule (OrdersV2Controller); status
    // transitions (accept/cancel/etc.) stay on the existing v1 order endpoints.
    [ApiController]
    [Route("api/v2/scheduled-orders")]
    public class ScheduledOrdersV2Controller : ControllerBase
    {
        private readonly IOrderV2QueryService _queryService;

        public ScheduledOrdersV2Controller(IOrderV2QueryService queryService)
        {
            _queryService = queryService;
        }

        // GET: api/v2/scheduled-orders
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orders = await _queryService.GetAllScheduledOrdersAsync();
            return Ok(orders);
        }

        // GET: api/v2/scheduled-orders/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _queryService.GetScheduledByIdAsync(id);
            if (order is null)
                return NotFound(new ApiResponse(404, "Scheduled order not found"));

            return Ok(order);
        }

        // GET: api/v2/scheduled-orders/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(string userId)
        {
            var orders = await _queryService.GetScheduledForUserAsync(userId);
            return Ok(orders);
        }

        // GET: api/v2/scheduled-orders/driver/{driverId}
        [HttpGet("driver/{driverId:int}")]
        public async Task<IActionResult> GetByDriver(int driverId)
        {
            var orders = await _queryService.GetScheduledForDriverAsync(driverId);
            return Ok(orders);
        }
    }
}
