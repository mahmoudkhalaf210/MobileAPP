using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    // v2-only, additive mapping for Order.CarTypeEnum. Does not touch the
    // legacy CarType string column or any other Order mapping.
    public class ConfigureOrderCarTypeV2 : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.Property(o => o.CarTypeEnum)
                .HasConversion<string>()
                .HasColumnType("nvarchar(32)")
                .IsRequired(false);
        }
    }
}
