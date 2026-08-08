using Microsoft.AspNetCore.Mvc;
using Snap.API.Errors;
using Snap.Application.Explore.DTOs;
using Snap.Application.Explore.Interfaces;

namespace Snap.API.Controllers.V2
{
    // Global explore-places catalog (photo + destination) — CRUD only, no real-time.
    [ApiController]
    [Route("api/v2/explore")]
    public class ExploreV2Controller : ControllerBase
    {
        private readonly IExplorePlaceService _service;

        public ExploreV2Controller(IExplorePlaceService service)
        {
            _service = service;
        }

        // POST: api/v2/explore
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertExplorePlaceDto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            var place = await _service.CreateAsync(dto);
            return Ok(place);
        }

        // GET: api/v2/explore
        [HttpGet]
        public async Task<ActionResult<List<ExplorePlaceDto>>> GetAll()
        {
            var places = await _service.GetAllAsync();
            return Ok(places);
        }

        // GET: api/v2/explore/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var place = await _service.GetByIdAsync(id);
            return place is null
                ? NotFound(new ApiResponse(404, "Place not found"))
                : Ok(place);
        }

        // PUT: api/v2/explore/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpsertExplorePlaceDto dto)
        {
            if (dto == null)
                return BadRequest(new ApiResponse(400, "Invalid payload"));

            var updated = await _service.UpdateAsync(id, dto);
            return updated is null
                ? NotFound(new ApiResponse(404, "Place not found"))
                : Ok(updated);
        }

        // DELETE: api/v2/explore/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            return deleted
                ? NoContent()
                : NotFound(new ApiResponse(404, "Place not found"));
        }
    }
}
