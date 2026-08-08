namespace Snap.Application.Common.DTOs
{
    public class PublicDriverInfoDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string? Photo { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? UserId { get; set; }
        public string? Status { get; set; }
        public double Wallet { get; set; }
        public int TotalReview { get; set; }
        public int NoReviews { get; set; }
        public string? Gender { get; set; }
    }
}
