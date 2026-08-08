using Microsoft.AspNetCore.Identity;

namespace Snap.Application.Domain.Entities
{
    public class User : IdentityUser
    {
        public string FullName { get; set; }
        public string UserType { get; set; }
        public string? Image { get; set; } = string.Empty;
        public string Gender { get; set; }
    }
}
