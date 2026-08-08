using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Users.DTOs
{
    public class VerifyOtpDto
    {
        [Required]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 4)]
        public string Otp { get; set; }
    }
}
