using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.CarDatas.DTOs;
using Snap.Application.CarDatas.Interfaces;

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
    }
}
