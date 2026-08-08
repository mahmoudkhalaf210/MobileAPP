using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Drivers.DTOs;
using Snap.Application.Drivers.Interfaces;

namespace Snap.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DriverController : ControllerBase
    {
        private readonly IDriverService _service;

        public DriverController(IDriverService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> CreateDriver([FromBody] CreateDriver dto)
        {
            try
            {
                var result = await _service.CreateDriverAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating driver: {ex.Message}"));
            }
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<DriverDto>> GetDriverByUserId(string userId)
        {
            try
            {
                var dto = await _service.GetDriverByUserIdAsync(userId);
                return Ok(dto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting driver: {ex.Message}"));
            }
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingDrivers()
        {
            try
            {
                var pendingDrivers = await _service.GetPendingDriversAsync();
                return Ok(pendingDrivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting pending drivers: {ex.Message}"));
            }
        }

        [HttpPut("{driverId}/status")]
        public async Task<IActionResult> ChangeDriverStatus(int driverId, [FromBody] ChangeDriverStatusDto dto)
        {
            try
            {
                await _service.ChangeDriverStatusAsync(driverId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse(400, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while changing driver status: {ex.Message}"));
            }
        }

        [HttpPost("{id}/review")]
        public async Task<IActionResult> AddReview(int id, [FromBody] AddDriverReviewDto dto)
        {
            try
            {
                await _service.AddReviewAsync(id, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse(400, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while adding review: {ex.Message}"));
            }
        }

        [HttpGet("{id}/review")]
        public async Task<IActionResult> GetDriverReview(int id)
        {
            try
            {
                var avg = await _service.GetDriverReviewAsync(id);
                return Ok(avg);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting driver review: {ex.Message}"));
            }
        }

        // POST: api/driver/requestcharge
        [HttpPost("requestcharge")]
        public async Task<IActionResult> RequestCharge([FromBody] RequestChargeDto dto)
        {
            try
            {
                var charge = await _service.RequestChargeAsync(dto);
                return Ok(charge);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while requesting charge: {ex.Message}"));
            }
        }

        // GET: api/driver/charges
        [HttpGet("charges")]
        public async Task<IActionResult> GetCharges()
        {
            try
            {
                var charges = await _service.GetChargesAsync();
                return Ok(charges);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting charges: {ex.Message}"));
            }
        }

        // POST: api/driver/charge/{id}/action
        [HttpPost("charge/{id}/action")]
        public async Task<IActionResult> HandleCharge(int id, [FromBody] ChargeActionDto dto)
        {
            try
            {
                var message = await _service.HandleChargeAsync(id, dto);
                return Ok(message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse(400, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while handling charge: {ex.Message}"));
            }
        }

        // PUT: api/driver/deductwallet
        [HttpPut("deductwallet")]
        public async Task<IActionResult> DeductFromWallet([FromBody] DeductWalletDto dto)
        {
            try
            {
                var newBalance = await _service.DeductFromWalletAsync(dto);
                return Ok(new { message = $"{dto.Amount} deducted from wallet. New balance: {newBalance}" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse(400, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while deducting from wallet: {ex.Message}"));
            }
        }

        [HttpGet("DriverId/{id}")]
        public async Task<ActionResult<DriverIdDto>> GetDriverByDriverId(int id)
        {
            try
            {
                var dto = await _service.GetDriverByDriverIdAsync(id);
                return Ok(dto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting driver by ID: {ex.Message}"));
            }
        }

        [HttpGet("approved")]
        public async Task<IActionResult> GetApprovedDrivers()
        {
            try
            {
                var result = await _service.GetApprovedDriversAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting approved drivers: {ex.Message}"));
            }
        }
    }
}
