using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Snap.Application.Domain.Entities;
using Snap.Infrastructure.Persistence;

namespace Snap.API.Extensions.Authentication
{
    public static class IdentityServicesExtension
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection services)
        {
            services.AddIdentity<User , IdentityRole>()
                .AddEntityFrameworkStores<SnapDbContext>()
                .AddDefaultTokenProviders();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

            return services;
        }
    }
}
