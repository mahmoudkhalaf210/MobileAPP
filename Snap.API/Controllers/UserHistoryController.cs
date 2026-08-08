using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.UserHistory.DTOs;
using Snap.Application.UserHistory.Interfaces;

namespace Snap.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserHistoryController : ControllerBase
    {
        private readonly IUserHistoryService _service;

        public UserHistoryController(IUserHistoryService service)
        {
            _service = service;
        }

        // POST: api/UserHistory
        [HttpPost]
        public async Task<IActionResult> CreateUserHistory([FromBody] CreateUserHistoryDto dto)
        {
            try
            {
                var result = await _service.CreateUserHistoryAsync(dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating user history: {ex.Message}"));
            }
        }

        // GET: api/UserHistory
        [HttpGet]
        public async Task<ActionResult<List<UserHistoryDto>>> GetAllUserHistories()
        {
            try
            {
                var histories = await _service.GetAllUserHistoriesAsync();
                return Ok(histories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting all user histories: {ex.Message}"));
            }
        }

        // GET: api/UserHistory/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<UserHistoryDetailsDto>>> GetUserHistoriesByUserId(string userId)
        {
            try
            {
                var histories = await _service.GetUserHistoriesByUserIdAsync(userId);
                return Ok(histories);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting user histories by user ID: {ex.Message}"));
            }
        }

        // GET: api/UserHistory/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<UserHistoryDto>> GetUserHistoryById(int id)
        {
            try
            {
                var dto = await _service.GetUserHistoryByIdAsync(id);
                return Ok(dto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting user history: {ex.Message}"));
            }
        }
    }
}
