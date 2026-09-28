using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    public class ConfigureDriverPointsTransaction : IEntityTypeConfiguration<DriverPointsTransaction>
    {
        public void Configure(EntityTypeBuilder<DriverPointsTransaction> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.DriverId).IsRequired();
            // Guarantees TryAwardPointsForOrderAsync is idempotent per order.
            builder.HasIndex(t => t.OrderId).IsUnique();
        }
    }
}
