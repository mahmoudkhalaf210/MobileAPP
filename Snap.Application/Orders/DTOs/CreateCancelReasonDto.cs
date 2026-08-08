using System.ComponentModel.DataAnnotations;

namespace Snap.Application.Orders.DTOs
{
    public class CreateCancelReasonDto
    {
        [Required]
        public string TextEn { get; set; } = null!;

        [Required]
        public string TextAr { get; set; } = null!;
    }
}
