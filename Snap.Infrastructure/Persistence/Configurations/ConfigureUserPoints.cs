using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    public class ConfigureUserPoints : IEntityTypeConfiguration<UserPoints>
    {
        public void Configure(EntityTypeBuilder<UserPoints> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.UserId).IsRequired();
            builder.HasIndex(p => p.UserId).IsUnique();
        }
    }
}
