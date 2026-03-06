using System.ComponentModel.DataAnnotations;

namespace Snap.APIs.DTOs
{
    public class FCMTokenDto
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public string Email { get; set; }
    }
}
