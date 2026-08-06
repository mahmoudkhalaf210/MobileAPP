using System.ComponentModel.DataAnnotations;

namespace Snap.Business.DTOs
{
    public class ExplorePlaceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Photo { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public class UpsertExplorePlaceDto
    {
        [Required] public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        /// <summary>Base64 string, stored verbatim — mirrors the existing User.Image upload pattern.</summary>
        public string? Photo { get; set; }
    }
}
