using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snap.APIs.DTOs;
using Snap.Core.Entities;
using Snap.Repository.Data;
using Snap.APIs.Errors;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Snap.APIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserHistoryController : ControllerBase
    {
        private readonly SnapDbContext _context;

        public UserHistoryController(SnapDbContext context)
        {
            _context = context;
        }

        // POST: api/UserHistory
        [HttpPost]
        public async Task<IActionResult> CreateUserHistory([FromBody] CreateUserHistoryDto dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(dto.UserId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found"));


                var history = new UserHistory
                {
                    UserId = dto.UserId,
                    From = dto.From,
                    To = dto.To,
                    Price = dto.Price,
                    Date = dto.Date,
                    PaymentMethod = dto.PaymentMethod,
                    RideType = dto.RideType
                };

                _context.UserHistories.Add(history);
                await _context.SaveChangesAsync();

                var result = new UserHistoryDto
                {
                    Id = history.Id,
                    UserId = history.UserId,
                    From = history.From,
                    To = history.To,
                    Price = history.Price,
                    Date = history.Date,
                    PaymentMethod = history.PaymentMethod,
                    RideType = history.RideType
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating user history: {ex.Message}"));
            }
        }

        // GET: api/UserHistory
        [HttpGet]
        public async Task<ActionResult<List<UserHistoryDto>>> GetAllUserHistories()
        {
            try
            {
                var histories = await _context.UserHistories
                    .Select(h => new UserHistoryDto
                    {
                        Id = h.Id,
                        UserId = h.UserId,
                        From = h.From,
                        To = h.To,
                        Price = h.Price,
                        Date = h.Date,
                        PaymentMethod = h.PaymentMethod,
                        RideType = h.RideType
                    })
                    .ToListAsync();

                return Ok(histories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting all user histories: {ex.Message}"));
            }
        }

        // GET: api/UserHistory/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<UserHistoryDetailsDto>>> GetUserHistoriesByUserId(string userId)
        {
            try
            {
                var histories = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.UserId == userId && o.Driverid != null)
                    .OrderByDescending(o => o.Date)
                    .Select(o => new UserHistoryDetailsDto
                    {
                        User = new PublicUserInfoDto
                        {
                            Id = o.User.Id,
                            FullName = o.User.FullName,
                            PhoneNumber = o.User.PhoneNumber,
                            Email = o.User.Email,
                            Image = o.User.Image,
                            Gender = o.User.Gender
                        },
                        Driver = _context.Drivers
                            .AsNoTracking()
                            .Where(d => d.Id == o.Driverid!.Value)
                            .Select(d => new PublicDriverInfoDto
                            {
                                Id = d.Id,
                                FullName = d.DriverFullname,
                                Photo = d.DriverPhoto,
                                PhoneNumber = d.User.PhoneNumber,
                                Email = d.User.Email,
                                UserId = d.UserId,
                                Status = d.Status,
                                Wallet = d.Wallet,
                                TotalReview = d.TotalReview,
                                NoReviews = d.NoReviews,
                                Gender = d.User.Gender
                            })
                            .FirstOrDefault(),
                        Trip = new TripDetailsDto
                        {
                            OrderId = o.Id,
                            Date = o.Date,
                            From = o.From,
                            To = o.To,
                            FromLatLng = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
                            ToLatLng = new LatLngDto { Lat = o.ToLatLng.Lat, Lng = o.ToLatLng.Lng },
                            ExpectedPrice = o.ExpectedPrice,
                            Budget = o.ExpectedPrice,
                            Fee = o.ExpectedPrice,
                            Type = o.Type,
                            Distance = o.Distance,
                            Notes = o.Notes,
                            NoPassengers = o.NoPassengers,
                            PaymentWay = o.PaymentWay,
                            CarType = o.CarType,
                            PinkMode = o.PinkMode,
                            Status = o.Status,
                            Review = o.Review
                        }
                    })
                    .ToListAsync();

                if (histories == null || histories.Count == 0)
                    return NotFound(new ApiResponse(404, "No trips found for this userId"));

                return Ok(histories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting user histories by user ID: {ex.Message}"));
            }
        }

        // GET: api/UserHistory/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<UserHistoryDto>> GetUserHistoryById(int id)
        {
            try
            {
                var history = await _context.UserHistories.FindAsync(id);
                if (history == null)
                    return NotFound(new ApiResponse(404, "User history not found"));

                var dto = new UserHistoryDto
                {
                    Id = history.Id,
                    UserId = history.UserId,
                    From = history.From,
                    To = history.To,
                    Price = history.Price,
                    Date = history.Date,
                    PaymentMethod = history.PaymentMethod,
                    RideType = history.RideType
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting user history: {ex.Message}"));
            }
        }
    }
}
