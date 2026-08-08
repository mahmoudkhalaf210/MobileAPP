using Microsoft.EntityFrameworkCore;
using Snap.Application.Common.DTOs;
using Snap.Application.Domain.Entities;
using Snap.Application.TripsHistory.DTOs;
using Snap.Application.TripsHistory.Interfaces;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class TripsHistoryRepository : ITripsHistoryRepository
    {
        private readonly SnapDbContext _context;

        public TripsHistoryRepository(SnapDbContext context)
        {
            _context = context;
        }

        public void Add(TripsHistory trip) => _context.TripsHistories.Add(trip);

        public Task<List<TripsHistory>> GetAllAsync() =>
            _context.TripsHistories.ToListAsync();

        public async Task<TripsHistory?> GetByIdAsync(int id) =>
            await _context.TripsHistories.FindAsync(id);

        public Task<Driver?> FindDriverByIdOrUserIdAsync(string driverIdOrUserId)
        {
            var isDriverId = int.TryParse(driverIdOrUserId, out var driverId);
            return _context.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => isDriverId ? d.Id == driverId : d.UserId == driverIdOrUserId);
        }

        public Task<List<DriverTripHistoryDetailsDto>> GetDriverTripHistoryDetailsAsync(int driverId) =>
            _context.Orders
                .AsNoTracking()
                .Where(o => o.Driverid == driverId)
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
    }
}
