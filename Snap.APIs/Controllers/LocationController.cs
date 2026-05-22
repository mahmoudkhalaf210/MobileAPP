using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snap.APIs.DTOs;
using Snap.APIs.Errors;
using Snap.APIs.Services;
using Snap.APIs.WebSockets;
using Snap.Repository.Data;

namespace Snap.APIs.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly SnapDbContext          _context;
        private readonly IDriverLocationService _locationService;
        private readonly IWebSocketHub          _hub;

        public LocationController(
            SnapDbContext          context,
            IDriverLocationService locationService,
            IWebSocketHub          hub)
        {
            _context         = context;
            _locationService = locationService;
            _hub             = hub;
        }

        // POST: api/location/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateDriverLocation([FromBody] DriverLocationDto locationDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var driver = await _context.Drivers
                    .AsNoTracking()
                    .Where(d => d.Id == locationDto.DriverId)
                    .Select(d => new { d.Id, d.DriverFullname })
                    .FirstOrDefaultAsync();

                if (driver == null)
                    return NotFound(new ApiResponse(404, "Driver not found"));

                _locationService.UpdateLocation(
                    locationDto.DriverId, driver.DriverFullname,
                    locationDto.Lat, locationDto.Lng);

                var updated = _locationService.GetDriverLocation(locationDto.DriverId)!;
                await _hub.BroadcastLocationEventAsync("LocationUpdate", updated);

                return Ok(new { message = "Location updated successfully", location = updated });
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
                var cached = _locationService.GetDriverLocation(driverId);
                if (cached != null)
                    return Ok(cached);

                var driver = await _context.Drivers
                    .AsNoTracking()
                    .Where(d => d.Id == driverId)
                    .Select(d => new { d.Id, d.DriverFullname })
                    .FirstOrDefaultAsync();

                if (driver == null)
                    return NotFound(new ApiResponse(404, "Driver not found"));

                return Ok(new DriverLocationResponseDto
                {
                    DriverId   = driver.Id,
                    DriverName = driver.DriverFullname,
                    IsOnline   = false
                });
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
                var snapshot = _locationService.GetDriverLocation(driverId);
                _locationService.RemoveDriver(driverId);

                if (snapshot != null)
                {
                    snapshot.IsOnline = false;
                    await _hub.BroadcastLocationEventAsync("DriverRemoved", new { driverId });
                }

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
