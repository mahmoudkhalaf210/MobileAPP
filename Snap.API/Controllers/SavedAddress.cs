using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Domain.Entities;
using Snap.Application.SavedAddresses.DTOs;
using Snap.Application.SavedAddresses.Interfaces;

namespace Snap.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SavedAddressController : ControllerBase
    {
        private readonly ISavedAddressService _service;

        public SavedAddressController(ISavedAddressService service)
        {
            _service = service;
        }

        // ADD ADDRESS
        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] SavedAddress dto)
        {
            try
            {
                var result = await _service.AddAddressAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        // GET ALL
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserAddresses(string userId)
        {
            try
            {
                var addresses = await _service.GetUserAddressesAsync(userId);
                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        // SUGGESTIONS
        [HttpGet("suggestions/{userId}")]
        public async Task<IActionResult> GetSuggestions(string userId)
        {
            try
            {
                var suggestions = await _service.GetSuggestionsAsync(userId);
                return Ok(suggestions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        [HttpGet("favorites/{userId}")]
        public async Task<IActionResult> GetFavorites(string userId)
        {
            try
            {
                var (home, work) = await _service.GetFavoritesAsync(userId);
                return Ok(new { home, work });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        [HttpPut("favorites/{userId}/{title}")]
        public async Task<IActionResult> UpsertFavorite(string userId, string title, [FromBody] UpsertFavoriteLocationDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new ApiResponse(400, "Invalid payload"));

                var entity = await _service.UpsertFavoriteAsync(userId, title, dto);
                return Ok(entity);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse(400, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        [HttpDelete("favorites/{userId}/{title}")]
        public async Task<IActionResult> DeleteFavorite(string userId, string title)
        {
            try
            {
                await _service.DeleteFavoriteAsync(userId, title);
                return Ok(new { message = "Favorite location deleted successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse(400, ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }
    }
}
