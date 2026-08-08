using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Users.DTOs
{
    public class LoginDto
    {
        [Required]
        public string EmailOrPhone { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
