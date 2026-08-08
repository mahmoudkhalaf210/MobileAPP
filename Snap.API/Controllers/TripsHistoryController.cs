using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.TripsHistory.DTOs;
using Snap.Application.TripsHistory.Interfaces;

namespace Snap.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripsHistoryController : ControllerBase
    {
        private readonly ITripsHistoryService _service;

        public TripsHistoryController(ITripsHistoryService service)
        {
            _service = service;
        }

        // POST: api/TripsHistory
        [HttpPost]
        public async Task<IActionResult> CreateTripsHistory([FromBody] CreateTripsHistoryDto dto)
        {
            try
            {
                var result = await _service.CreateTripsHistoryAsync(dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating trip history: {ex.Message}"));
            }
        }

        // GET: api/TripsHistory
        [HttpGet]
        public async Task<ActionResult<List<TripsHistoryDto>>> GetAllTripsHistories()
        {
            try
            {
                var trips = await _service.GetAllTripsHistoriesAsync();
                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting all trips histories: {ex.Message}"));
            }
        }

        // GET: api/TripsHistory/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<TripsHistoryDto>> GetTripsHistoryById(int id)
        {
            try
            {
                var dto = await _service.GetTripsHistoryByIdAsync(id);
                return Ok(dto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting trip history: {ex.Message}"));
            }
        }

        // GET: api/TripsHistory/driver/{driverIdOrUserId}
        [HttpGet("driver/{driverIdOrUserId}")]
        public async Task<ActionResult<List<DriverTripHistoryDetailsDto>>> GetTripsHistoriesByUserId(string driverIdOrUserId)
        {
            try
            {
                var trips = await _service.GetTripsHistoriesByUserIdAsync(driverIdOrUserId);
                return Ok(trips);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting trips histories by user ID: {ex.Message}"));
            }
        }
    }
}
