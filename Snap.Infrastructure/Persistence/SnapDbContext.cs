using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Snap.Application.Domain.Entities;
using System.Reflection;

namespace Snap.Infrastructure.Persistence
{
    public class SnapDbContext: IdentityDbContext<User>
    {
        public SnapDbContext(DbContextOptions<SnapDbContext>options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            // Configure LatLng as owned type for Order
            modelBuilder.Entity<Order>().OwnsOne(o => o.FromLatLng);
            modelBuilder.Entity<Order>().OwnsOne(o => o.ToLatLng);
            // Soft-deleted orders are hidden from all LINQ queries; use IgnoreQueryFilters() to see them.
            modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
            base.OnModelCreating(modelBuilder);
        }

        // DbSet properties for each entity
        public DbSet<User> Users { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<CarData> CarDatas { get; set; }
        public DbSet<Charge> Charges { get; set; }
        public DbSet<TripsHistory> TripsHistories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<UserHistory> UserHistories { get; set; }
        public DbSet<FCMTokenUser> FCMTokenUsers { get; set; }

        public DbSet<AppVersion> AppVersions { get; set; }
        public DbSet<SavedAddress> SavedAddresses { get; set; }

        // v2 additions
        public DbSet<ExplorePlace> ExplorePlaces { get; set; }
        public DbSet<UserPoints> UserPoints { get; set; }
        public DbSet<UserPointsTransaction> UserPointsTransactions { get; set; }
        public DbSet<DriverPoints> DriverPoints { get; set; }
        public DbSet<DriverPointsTransaction> DriverPointsTransactions { get; set; }
        public DbSet<CancelReason> CancelReasons { get; set; }
    }
}
