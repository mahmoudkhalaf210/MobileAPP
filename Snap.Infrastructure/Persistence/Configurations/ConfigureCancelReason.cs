using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    public class ConfigureCancelReason : IEntityTypeConfiguration<CancelReason>
    {
        public void Configure(EntityTypeBuilder<CancelReason> builder)
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.TextEn).IsRequired();
            builder.Property(r => r.TextAr).IsRequired();
        }
    }
}
