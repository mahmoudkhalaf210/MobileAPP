using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snap.APIs.DTOs;
using Snap.Core.Entities;
using Snap.Repository.Data;
using Snap.APIs.Errors;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Snap.APIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripsHistoryController : ControllerBase
    {
        private readonly SnapDbContext _context;

        public TripsHistoryController(SnapDbContext context)
        {
            _context = context;
        }

        // POST: api/TripsHistory
        [HttpPost]
        public async Task<IActionResult> CreateTripsHistory([FromBody] CreateTripsHistoryDto dto)
        {
            try
            {
                var driver = await _context.Drivers.FindAsync(dto.DriverId);
                if (driver == null)
                    return NotFound(new ApiResponse(404, "Driver not found"));

                var trip = new TripsHistory
                {
                    Review = dto.Review,
                    PaymentWay = dto.PaymentWay,
                    From = dto.From,
                    To = dto.To,
                    Date = dto.Date,
                    TotalTip = dto.TotalTip,
                    DriverId = dto.DriverId
                };

                _context.TripsHistories.Add(trip);
                await _context.SaveChangesAsync();

                var result = new TripsHistoryDto
                {
                    Id = trip.Id,
                    Review = trip.Review,
                    PaymentWay = trip.PaymentWay,
                    From = trip.From,
                    To = trip.To,
                    Date = trip.Date,
                    TotalTip = trip.TotalTip,
                    DriverId = trip.DriverId
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while creating trip history: {ex.Message}"));
            }
        }

        // GET: api/TripsHistory
        [HttpGet]
        public async Task<ActionResult<List<TripsHistoryDto>>> GetAllTripsHistories()
        {
            try
            {
                var trips = await _context.TripsHistories
                    .Select(t => new TripsHistoryDto
                    {
                        Id = t.Id,
                        Review = t.Review,
                        PaymentWay = t.PaymentWay,
                        From = t.From,
                        To = t.To,
                        Date = t.Date,
                        TotalTip = t.TotalTip,
                        DriverId = t.DriverId
                    })
                    .ToListAsync();

                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting all trips histories: {ex.Message}"));
            }
        }

        // GET: api/TripsHistory/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<TripsHistoryDto>> GetTripsHistoryById(int id)
        {
            try
            {
                var trip = await _context.TripsHistories.FindAsync(id);
                if (trip == null)
                    return NotFound(new ApiResponse(404, "Trip history not found"));

                var dto = new TripsHistoryDto
                {
                    Id = trip.Id,
                    Review = trip.Review,
                    PaymentWay = trip.PaymentWay,
                    From = trip.From,
                    To = trip.To,
                    Date = trip.Date,
                    TotalTip = trip.TotalTip,
                    DriverId = trip.DriverId
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting trip history: {ex.Message}"));
            }
        }

        // GET: api/TripsHistory/driver/{driverIdOrUserId}
        [HttpGet("driver/{driverIdOrUserId}")]
        public async Task<ActionResult<List<DriverTripHistoryDetailsDto>>> GetTripsHistoriesByUserId(string driverIdOrUserId)
        {
            try
            {
                var isDriverId = int.TryParse(driverIdOrUserId, out var driverId);
                var driver = await _context.Drivers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => isDriverId ? d.Id == driverId : d.UserId == driverIdOrUserId);

                if (driver == null)
                    return NotFound(new ApiResponse(404, "Driver not found"));

                var trips = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.Driverid == driver.Id)
                    .OrderByDescending(o => o.Date)
                    .Select(o => new DriverTripHistoryDetailsDto
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
                            .FirstOrDefault()!,
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

                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting trips histories by user ID: {ex.Message}"));
            }
        }
    }
}
