using Snap.Application.Domain.Entities;
using Snap.Application.Drivers.Models;

namespace Snap.Application.Drivers.Interfaces
{
    // Driver-domain persistence: Driver, its owning User, its CarData, and Charges.
    // Cross-entity joins (Driver+User+CarData) don't fit the generic IRepository<T>
    // shape, so this dedicated repository covers the exact queries DriverController
    // used to run inline.
    public interface IDriverRepository
    {
        void Add(Driver driver);
        Task<Driver?> GetByUserIdAsync(string userId);
        Task<Driver?> GetTrackedByIdAsync(int id);
        Task<List<Driver>> GetPendingAsync();
        Task<List<DriverWithDetails>> GetApprovedWithDetailsAsync();

        Task<User?> GetUserByIdAsync(string userId);
        Task<CarData?> GetCarDataByDriverIdAsync(int driverId);
        Task<string?> GetDriverNameByIdAsync(int driverId);

        void AddCharge(Charge charge);
        Task<List<Charge>> GetChargesWithDriverAsync();
        Task<Charge?> GetChargeWithDriverAsync(int id);
        void RemoveCharge(Charge charge);
    }
}
