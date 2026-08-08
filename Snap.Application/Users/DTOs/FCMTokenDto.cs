using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Users.DTOs
{
    public class FCMTokenDto
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public string Email { get; set; }
    }
}
