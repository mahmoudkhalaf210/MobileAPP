using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Snap.Application.Domain.Entities;

namespace Snap.Infrastructure.Persistence.Configurations
{
    public class ConfigureUserExtend : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {




        }
    }
}
