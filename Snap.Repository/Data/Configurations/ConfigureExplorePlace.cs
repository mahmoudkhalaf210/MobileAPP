using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Core.Entities;

namespace Snap.Repository.Data.Configurations
{
    public class ConfigureExplorePlace : IEntityTypeConfiguration<ExplorePlace>
    {
        public void Configure(EntityTypeBuilder<ExplorePlace> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).IsRequired();
            builder.Property(p => p.Description).IsRequired(false);
            builder.Property(p => p.Photo).IsRequired(false);
        }
    }
}
