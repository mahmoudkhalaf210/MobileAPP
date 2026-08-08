using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.AppVersions.Interfaces;

namespace Snap.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppVersionController : ControllerBase
    {
        private readonly IAppVersionService _service;

        public AppVersionController(IAppVersionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> CheckVersion([FromQuery] string currentVersion, [FromQuery] string platform)
        {
            try
            {
                var result = await _service.CheckVersionAsync(currentVersion, platform);

                if (result == null)
                    return NotFound(new ApiResponse(404, "Version config not found"));

                return Ok(new
                {
                    latestVersion = result.LatestVersion,
                    minSupportedVersion = result.MinSupportedVersion,
                    forceUpdate = result.ForceUpdate,
                    updateUrl = result.UpdateUrl,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }
    }
}
