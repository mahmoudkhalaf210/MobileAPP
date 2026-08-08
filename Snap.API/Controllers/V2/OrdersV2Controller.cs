using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;

namespace Snap.API.Controllers.V2
{
    // v2 order creation only — split into separate normal/scheduled endpoints.
    // Post-creation lifecycle (accept/arrive/start/complete/cancel) and all "get
    // orders" reads stay on the existing PUT api/orders/driver endpoint and the
    // new OrdersHubV2 (SignalR) respectively; nothing here duplicates that.
    [ApiController]
    [Route("api/v2/orders")]
    public class OrdersV2Controller : ControllerBase
    {
        private readonly IOrderV2CommandService _commandService;

        public OrdersV2Controller(IOrderV2CommandService commandService)
        {
            _commandService = commandService;
        }

        // POST: api/v2/orders/normal
        [HttpPost("normal")]
        public async Task<IActionResult> CreateNormal([FromBody] CreateNormalOrderV2Dto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                var order = await _commandService.CreateNormalAsync(dto);
                return Ok(order);
            }
            catch (ArgumentException ex)    { return BadRequest(new ApiResponse(400, ex.Message)); }
            catch (KeyNotFoundException ex) { return NotFound(new ApiResponse(404, ex.Message)); }
            catch (Exception ex)            { return StatusCode(500, new ApiResponse(500, ex.Message)); }
        }

        // POST: api/v2/orders/schedule
        [HttpPost("schedule")]
        public async Task<IActionResult> CreateScheduled([FromBody] CreateScheduledOrderV2Dto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            try
            {
                var order = await _commandService.CreateScheduledAsync(dto);
                return Ok(order);
            }
            catch (ArgumentException ex)    { return BadRequest(new ApiResponse(400, ex.Message)); }
            catch (KeyNotFoundException ex) { return NotFound(new ApiResponse(404, ex.Message)); }
            catch (Exception ex)            { return StatusCode(500, new ApiResponse(500, ex.Message)); }
        }
    }
}
