using Microsoft.AspNetCore.Mvc;
using Snap.APIs.Errors;
using Snap.Core.Entities;
using Snap.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Snap.APIs.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SavedAddressController : ControllerBase
    {
        private readonly SnapDbContext _context;

        public SavedAddressController(SnapDbContext context)
        {
            _context = context;
        }

        // ADD ADDRESS
        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] SavedAddress dto)
        {
            try
            {
                _context.SavedAddresses.Add(dto);
                await _context.SaveChangesAsync();

                return Ok(dto);
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
                var addresses = await _context.SavedAddresses
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();

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
                var suggestions = await _context.SavedAddresses
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.UsageCount)
                    .Take(5)
                    .ToListAsync();

                return Ok(suggestions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }
    }
}
