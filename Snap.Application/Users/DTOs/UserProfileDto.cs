namespace Snap.Application.Users.DTOs
{
    public class UserProfileDto
    {
        public string UserId { get; set; } = null!;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Image { get; set; }
        public string? UserType { get; set; }
        public string? Gender { get; set; }
        public EmergencyContactDto? EmergencyContact { get; set; }
    }
}
