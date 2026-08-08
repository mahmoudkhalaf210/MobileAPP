using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Drivers.DTOs;
using Snap.Application.Drivers.Interfaces;

namespace Snap.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly IDriverLocationService _locationService;
        private readonly ILocationService _service;

        public LocationController(
            IDriverLocationService locationService,
            ILocationService service)
        {
            _locationService = locationService;
            _service         = service;
        }

        // POST: api/location/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateDriverLocation([FromBody] DriverLocationDto locationDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var updated = await _service.UpdateDriverLocationAsync(locationDto);

                return Ok(new { message = "Location updated successfully", location = updated });
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

        // GET: api/location/drivers
        [HttpGet("drivers")]
        public IActionResult GetOnlineDrivers()
        {
            try
            {
                return Ok(_locationService.GetConnectedDrivers());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        // GET: api/location/driver/{driverId}
        [HttpGet("driver/{driverId}")]
        public async Task<IActionResult> GetDriverLocation(int driverId)
        {
            try
            {
                var location = await _service.GetDriverLocationOrFallbackAsync(driverId);
                return Ok(location);
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

        // GET: api/location/nearby?lat=&lng=&radiusKm=
        [HttpGet("nearby")]
        public IActionResult GetNearbyDrivers(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 10)
        {
            try
            {
                if (lat < -90  || lat > 90)  return BadRequest(new ApiResponse(400, "Lat must be between -90 and 90"));
                if (lng < -180 || lng > 180) return BadRequest(new ApiResponse(400, "Lng must be between -180 and 180"));

                var nearby = _locationService.GetConnectedDrivers()
                    .Where(d => Haversine(lat, lng, d.Lat, d.Lng) <= radiusKm)
                    .OrderBy(d => Haversine(lat, lng, d.Lat, d.Lng))
                    .ToList();

                return Ok(nearby);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        // DELETE: api/location/driver/{driverId}
        [HttpDelete("driver/{driverId}")]
        public async Task<IActionResult> RemoveDriverLocation(int driverId)
        {
            try
            {
                await _service.RemoveDriverLocationAsync(driverId);
                return Ok(new { message = "Driver location removed", driverId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, ex.Message));
            }
        }

        private static double Haversine(double lat1, double lng1, double lat2, double lng2)
        {
            const double R    = 6371;
            var          dLat = ToRad(lat2 - lat1);
            var          dLng = ToRad(lng2 - lng1);
            var          a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }
}
