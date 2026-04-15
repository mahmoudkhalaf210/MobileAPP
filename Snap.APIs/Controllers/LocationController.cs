using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.SignalR; // Removed - Using WebSocket instead
using Snap.APIs.DTOs;
// using Snap.APIs.Hubs; // Removed - Using WebSocket instead
using Snap.APIs.Errors;
using Snap.Repository.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System;
using Snap.APIs.Services;

namespace Snap.APIs.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly SnapDbContext _context;
        private readonly IDriverLocationService _locationService;

        public LocationController(SnapDbContext context, IDriverLocationService locationService)
        {
            _context = context;
            _locationService = locationService;
        }

        // POST: api/location/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateDriverLocation([FromBody] DriverLocationDto locationDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Verify driver exists
                var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == locationDto.DriverId);
                if (driver == null)
                    return NotFound(new ApiResponse(404, "Driver not found"));

                // Update location in service
                _locationService.UpdateLocation(locationDto.DriverId, driver.DriverFullname, locationDto.Lat, locationDto.Lng);

                // Create response dto for WebSocket
                var driverLocation = new DriverLocationResponseDto
                {
                    DriverId = locationDto.DriverId,
                    DriverName = driver.DriverFullname,
                    Lat = locationDto.Lat,
                    Lng = locationDto.Lng,
                    LastUpdate = locationDto.Timestamp,
                    IsOnline = true
                };

                // Update WebSocket middleware cache (Legacy/Current implementation support)
                Snap.APIs.Middlewares.WebSocketMiddleware.UpdateDriverLocation(driverLocation);

                // Broadcast location update to all WebSocket clients
                await Snap.APIs.Middlewares.WebSocketMiddleware.BroadcastLocationUpdate(driverLocation);

                return Ok(new { message = "Location updated successfully", location = driverLocation });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating driver location: {ex.Message}"));
            }
        }

        // GET: api/location/drivers
        [HttpGet("drivers")]
        public IActionResult GetOnlineDrivers()
        {
            try
            {
                var onlineDrivers = _locationService.GetOnlineDrivers();
                return Ok(onlineDrivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting online drivers: {ex.Message}"));
            }
        }

        // GET: api/location/driver/{driverId}
        [HttpGet("driver/{driverId}")]
        public async Task<IActionResult> GetDriverLocation(int driverId)
        {
            try
            {
                var driverLocation = _locationService.GetDriverLocation(driverId);
                if (driverLocation != null)
                {
                    return Ok(driverLocation);
                }

                var driver = await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId);
                if (driver == null)
                {
                    return NotFound(new ApiResponse(404, "Driver not found"));
                }

                return Ok(new DriverLocationResponseDto
                {
                    DriverId = driver.Id,
                    DriverName = driver.DriverFullname,
                    Lat = 0,
                    Lng = 0,
                    LastUpdate = DateTime.MinValue,
                    IsOnline = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting driver location: {ex.Message}"));
            }
        }

        // GET: api/location/nearby
        [HttpGet("nearby")]
        public IActionResult GetNearbyDrivers([FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm = 10)
        {
            try
            {
                if (lat < -90 || lat > 90)
                    return BadRequest(new ApiResponse(400, "Lat must be between -90 and 90"));

                if (lng < -180 || lng > 180)
                    return BadRequest(new ApiResponse(400, "Lng must be between -180 and 180"));

                var nearbyDrivers = _locationService.GetOnlineDrivers()
                    .Where(d => CalculateDistance(lat, lng, d.Lat, d.Lng) <= radiusKm)
                    .OrderBy(d => CalculateDistance(lat, lng, d.Lat, d.Lng))
                    .ToList();

                return Ok(nearbyDrivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting nearby drivers: {ex.Message}"));
            }
        }

        // DELETE: api/location/driver/{driverId}
        [HttpDelete("driver/{driverId}")]
        public async Task<IActionResult> RemoveDriverLocation(int driverId)
        {
            try
            {
                _locationService.RemoveDriver(driverId);
                
                // Notify all WebSocket clients that driver was removed
                await Snap.APIs.Middlewares.WebSocketMiddleware.BroadcastDriverRemoved(driverId);

                return Ok(new { message = "Driver location removed", driverId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while removing driver location: {ex.Message}"));
            }
        }

        // Helper method to calculate distance between two points
        private static double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadius = 6371; // Earth's radius in kilometers

            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
    }
}
