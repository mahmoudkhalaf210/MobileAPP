using Microsoft.AspNetCore.Mvc;
using Snap.APIs.Errors;
using Snap.APIs.DTOs;
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

        [HttpGet("favorites/{userId}")]
        public async Task<IActionResult> GetFavorites(string userId)
        {
            try
            {
                var favorites = await _context.SavedAddresses
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && (x.Title == "Home" || x.Title == "Work"))
                    .ToListAsync();

                var home = favorites.FirstOrDefault(x => x.Title == "Home");
                var work = favorites.FirstOrDefault(x => x.Title == "Work");

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

                var normalizedTitle = NormalizeFavoriteTitle(title);
                if (normalizedTitle == null)
                    return BadRequest(new ApiResponse(400, "Title must be Home or Work."));

                var existing = await _context.SavedAddresses
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Title == normalizedTitle);

                if (existing == null)
                {
                    var entity = new SavedAddress
                    {
                        UserId = userId,
                        Title = normalizedTitle,
                        AddressLine = dto.AddressLine,
                        Latitude = dto.Latitude,
                        Longitude = dto.Longitude,
                        CreatedAt = DateTime.UtcNow,
                        UsageCount = 0
                    };

                    _context.SavedAddresses.Add(entity);
                    await _context.SaveChangesAsync();
                    return Ok(entity);
                }

                existing.AddressLine = dto.AddressLine;
                existing.Latitude = dto.Latitude;
                existing.Longitude = dto.Longitude;
                await _context.SaveChangesAsync();

                return Ok(existing);
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
                var normalizedTitle = NormalizeFavoriteTitle(title);
                if (normalizedTitle == null)
                    return BadRequest(new ApiResponse(400, "Title must be Home or Work."));

                var existing = await _context.SavedAddresses
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Title == normalizedTitle);

                if (existing == null)
                    return NotFound(new ApiResponse(404, "Favorite location not found."));

                _context.SavedAddresses.Remove(existing);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Favorite location deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        private static string? NormalizeFavoriteTitle(string title)
        {
            if (string.Equals(title, "home", StringComparison.OrdinalIgnoreCase))
                return "Home";
            if (string.Equals(title, "work", StringComparison.OrdinalIgnoreCase))
                return "Work";
            return null;
        }
    }
}
