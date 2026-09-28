using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    public class ConfigureDriverPoints : IEntityTypeConfiguration<DriverPoints>
    {
        public void Configure(EntityTypeBuilder<DriverPoints> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.DriverId).IsRequired();
            builder.HasIndex(p => p.DriverId).IsUnique();
        }
    }
}
