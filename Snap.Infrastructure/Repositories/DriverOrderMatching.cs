using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Entities;
using Snap.Application.Domain.Enums;
using Snap.Infrastructure.Persistence;

namespace Snap.Infrastructure.Repositories
{
    /// <summary>
    /// Single source of truth for "which driver may see / receive / accept which order",
    /// shared by FCM, WebSocket dispatch, accept validation and driver-facing order lists.
    ///
    /// Rules:
    ///   • Car type — the driver's type lives in CarData.CarBrand ("Car"/"Scooter"/
    ///     "SuperMalaky"/"Taxi"/"Lada"); it must equal the order's CarType.
    ///     A driver with no CarBrand yet matches every type. An order whose CarType
    ///     isn't a known CarType name (free-text v1 orders) matches every driver.
    ///   • Pink Mode — on top of the car type, only female drivers.
    /// </summary>
    internal static class DriverOrderMatching
    {
        private static readonly string[] CarTypeNames = Enum.GetNames<CarType>();

        public sealed record DriverProfile(string? CarBrand, bool IsFemale);

        public static IQueryable<Driver> WhereEligible(
            this IQueryable<Driver> query, SnapDbContext context, CarType? carType, bool pinkMode)
        {
            if (pinkMode)
                query = query.Where(d => context.Users.Any(u => u.Id == d.UserId && u.Gender != null && u.Gender.ToLower() == "female"));

            if (carType.HasValue)
            {
                var wanted = carType.Value.ToString();
                query = query.Where(d =>
                    !context.CarDatas.Any(c => c.DriverId == d.Id && c.CarBrand != null && c.CarBrand != "") ||
                    context.CarDatas.Any(c => c.DriverId == d.Id && c.CarBrand == wanted));
            }

            return query;
        }

        public static async Task<DriverProfile> GetProfileAsync(SnapDbContext context, int driverId, CancellationToken ct = default)
        {
            var carBrand = await context.CarDatas
                .AsNoTracking()
                .Where(c => c.DriverId == driverId && c.CarBrand != null && c.CarBrand != "")
                .Select(c => c.CarBrand)
                .FirstOrDefaultAsync(ct);

            var isFemale = await context.Drivers
                .AsNoTracking()
                .Where(d => d.Id == driverId)
                .Join(context.Users, d => d.UserId, u => u.Id, (_, u) => u.Gender)
                .AnyAsync(g => g != null && g.ToLower() == "female", ct);

            return new DriverProfile(carBrand, isFemale);
        }

        public static IQueryable<Order> WhereVisibleTo(this IQueryable<Order> query, DriverProfile driver)
        {
            if (!driver.IsFemale)
                query = query.Where(o => !o.PinkMode);

            if (!string.IsNullOrEmpty(driver.CarBrand))
            {
                var brand = driver.CarBrand;
                query = query.Where(o => o.CarType == brand || !CarTypeNames.Contains(o.CarType));
            }

            return query;
        }
    }
}
