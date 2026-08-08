using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Entities;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Drivers.Models;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    public class DriverRepository : IDriverRepository
    {
        private readonly SnapDbContext _context;

        public DriverRepository(SnapDbContext context)
        {
            _context = context;
        }

        public void Add(Driver driver) => _context.Drivers.Add(driver);

        public Task<Driver?> GetByUserIdAsync(string userId) =>
            _context.Drivers.FirstOrDefaultAsync(d => d.UserId == userId);

        public async Task<Driver?> GetTrackedByIdAsync(int id) => await _context.Drivers.FindAsync(id);

        public Task<List<Driver>> GetPendingAsync() =>
            _context.Drivers.Where(d => d.Status == "pending").ToListAsync();

        public async Task<List<DriverWithDetails>> GetApprovedWithDetailsAsync()
        {
            var approvedDrivers = await _context.Drivers
                .Where(d => d.Status == "approved")
                .Select(d => new
                {
                    Driver = d,
                    User = _context.Users.FirstOrDefault(u => u.Id == d.UserId),
                    CarData = _context.CarDatas.FirstOrDefault(c => c.DriverId == d.Id)
                })
                .ToListAsync();

            return approvedDrivers
                .Select(item => new DriverWithDetails { Driver = item.Driver, User = item.User, CarData = item.CarData })
                .ToList();
        }

        public Task<User?> GetUserByIdAsync(string userId) =>
            _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        public Task<CarData?> GetCarDataByDriverIdAsync(int driverId) =>
            _context.CarDatas.FirstOrDefaultAsync(c => c.DriverId == driverId);

        public Task<string?> GetDriverNameByIdAsync(int driverId) =>
            _context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Select(d => d.DriverFullname)
                .FirstOrDefaultAsync();

        public void AddCharge(Charge charge) => _context.Charges.Add(charge);

        public Task<List<Charge>> GetChargesWithDriverAsync() =>
            _context.Charges.Include(c => c.Driver).ToListAsync();

        public Task<Charge?> GetChargeWithDriverAsync(int id) =>
            _context.Charges.Include(c => c.Driver).FirstOrDefaultAsync(c => c.Id == id);

        public void RemoveCharge(Charge charge) => _context.Charges.Remove(charge);
    }
}
