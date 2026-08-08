using Microsoft.EntityFrameworkCore;
using Snap.Application.Common.DTOs;
using Snap.Application.Domain.Entities;
using Snap.Application.UserHistory.DTOs;
using Snap.Application.UserHistory.Interfaces;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class UserHistoryRepository : IUserHistoryRepository
    {
        private readonly SnapDbContext _context;

        public UserHistoryRepository(SnapDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(string userId) => await _context.Users.FindAsync(userId);

        public void Add(UserHistory history) => _context.UserHistories.Add(history);

        public Task<List<UserHistory>> GetAllAsync() =>
            _context.UserHistories.ToListAsync();

        public async Task<UserHistory?> GetByIdAsync(int id) =>
            await _context.UserHistories.FindAsync(id);

        public Task<List<UserHistoryDetailsDto>> GetUserHistoryDetailsAsync(string userId) =>
            _context.Orders
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
    }
}
