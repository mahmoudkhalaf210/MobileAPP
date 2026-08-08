using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;

namespace Snap.API.Controllers.V2
{
    // Cancellation reasons catalog (English + Arabic) selected by the user when
    // cancelling an order via PUT api/orders/user/cancel (CancelOrderByUserDto.CancelReasonId).
    [ApiController]
    [Route("api/v2/cancel-reasons")]
    public class CancelReasonsV2Controller : ControllerBase
    {
        private readonly ICancelReasonService _service;

        public CancelReasonsV2Controller(ICancelReasonService service)
        {
            _service = service;
        }

        // POST: api/v2/cancel-reasons
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCancelReasonDto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            var reason = await _service.CreateAsync(dto);
            return Ok(reason);
        }

        // GET: api/v2/cancel-reasons
        [HttpGet]
        public async Task<ActionResult<List<CancelReasonDto>>> GetAll()
        {
            var reasons = await _service.GetAllAsync();
            return Ok(reasons);
        }
    }
}
