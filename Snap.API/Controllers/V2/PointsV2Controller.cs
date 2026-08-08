using Microsoft.AspNetCore.Mvc;
using Snap.Application.Points.DTOs;
using Snap.Application.Points.Interfaces;

namespace Snap.API.Controllers.V2
{
    // Minimal read surface for the points system — points are awarded automatically
    // (OrderNotificationServiceV2Decorator, on order completion), not via this controller.
    [ApiController]
    [Route("api/v2/points")]
    public class PointsV2Controller : ControllerBase
    {
        private readonly IPointsService _service;

        public PointsV2Controller(IPointsService service)
        {
            _service = service;
        }

        // GET: api/v2/points/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<UserPointsBalanceDto>> GetBalance(string userId)
        {
            var balance = await _service.GetBalanceAsync(userId);
            return Ok(new UserPointsBalanceDto { UserId = userId, Balance = balance });
        }
    }
}
