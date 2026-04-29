using Microsoft.AspNetCore.Mvc;
using Snap.APIs.Errors;
using Snap.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Snap.APIs.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppVersionController : ControllerBase
    {
        private readonly SnapDbContext _context;

        public AppVersionController(SnapDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> CheckVersion([FromQuery] string currentVersion, [FromQuery] string platform)
        {
            try
            {
                var version = await _context.AppVersions
                    .FirstOrDefaultAsync(x => x.Platform == platform);

                if (version == null)
                    return NotFound(new ApiResponse(404, "Version config not found"));

                var userVersion = new Version(currentVersion);
                var minVersion = new Version(version.MinSupportedVersion);

                bool forceUpdate = userVersion < minVersion;

                return Ok(new
                {
                    latestVersion = version.LatestVersion,
                    minSupportedVersion = version.MinSupportedVersion,
                    forceUpdate = forceUpdate,
                    updateUrl = version.UpdateUrl,
                    message = version.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }
    }
}
