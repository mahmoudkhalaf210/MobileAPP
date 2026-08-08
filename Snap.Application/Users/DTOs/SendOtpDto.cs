using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Users.DTOs
{
    public class SendOtpDto
    {
        [Required]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public string PhoneNumber { get; set; }
    }
}
