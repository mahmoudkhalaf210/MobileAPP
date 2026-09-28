using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.CarDatas.DTOs;
using Snap.Application.CarDatas.Interfaces;
using Snap.Application.Domain.Enums;

namespace Snap.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarDataController : ControllerBase
    {
        private readonly ICarDataService _service;

        public CarDataController(ICarDataService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> CreateCarData([FromBody] CarDataDto dto)
        {
            try
            {
                var result = await _service.CreateCarDataAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating car data: {ex.Message}"));
            }
        }

        [HttpGet("by-driver/{driverId}")]
        public async Task<ActionResult<CarDataDto>> GetCarDataByDriverId(int driverId)
        {
            try
            {
                var dto = await _service.GetCarDataByDriverIdAsync(driverId);
                if (dto == null) return NotFound(new ApiResponse(404, "Car data not found"));
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting car data: {ex.Message}"));
            }
        }

        // PUT: api/CarData/by-driver/{driverId}/car-type
        [HttpPut("by-driver/{driverId}/car-type")]
        public async Task<ActionResult<CarDataDto>> UpdateCarType(int driverId, [FromBody] UpdateCarTypeDto dto)
        {
            try
            {
                var result = await _service.UpdateCarTypeAsync(driverId, dto.CarType);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating car type: {ex.Message}"));
            }
        }
    }

    public class UpdateCarTypeDto
    {
        public CarType CarType { get; set; }
    }
}
