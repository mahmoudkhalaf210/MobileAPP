﻿namespace Snap.APIs.DTOs
{
    public class UserDto
    {
        public string UserId { get; set; }
        public string DispalyName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Token { get; set; }
        public string UserType { get; set; }
        public string Gender { get; set; }
    }

    public class UpdateUserProfileDto
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Image { get; set; }
    }

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

    public class EmergencyContactDto
    {
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class UpsertFavoriteLocationDto
    {
        public string AddressLine { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
