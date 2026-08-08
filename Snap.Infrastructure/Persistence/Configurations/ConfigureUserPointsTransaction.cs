using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    public class ConfigureUserPointsTransaction : IEntityTypeConfiguration<UserPointsTransaction>
    {
        public void Configure(EntityTypeBuilder<UserPointsTransaction> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.UserId).IsRequired();
            // Guarantees TryAwardPointsForOrderAsync is idempotent per order — the guard the
            // repository's raw-SQL insert relies on, not just an app-level convention.
            builder.HasIndex(t => t.OrderId).IsUnique();
        }
    }
}
