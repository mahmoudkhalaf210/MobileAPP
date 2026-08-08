using Microsoft.AspNetCore.Identity;
using Snap.Application.Domain.Entities;
using System.Threading.Tasks;

namespace Snap.Application.Identity.Interfaces
{
    public interface ITokenService
    {
        Task<string> CreateTokenAsync(User user , UserManager<User> userManager);

    }
}
