using System.ComponentModel.DataAnnotations;
using Snap.Application.Domain.Enums;

namespace Snap.Application.Orders.DTOs
{
    public class CreateNormalOrderV2Dto
    {
        [Required] public string UserId { get; set; } = null!;
        [Required] public string From { get; set; } = null!;
        [Required] public string To { get; set; } = null!;
        [Required] public LatLngV2Dto FromLatLng { get; set; } = new();
        [Required] public LatLngV2Dto ToLatLng { get; set; } = new();
        public double ExpectedPrice { get; set; }
        [Required] public string Type { get; set; } = null!;
        public double Distance { get; set; }
        public string? Notes { get; set; }
        public int NoPassengers { get; set; }
        public string? PaymentWay { get; set; }
        public CarType CarType { get; set; }
        public bool PinkMode { get; set; }
        public string? FCMToken { get; set; }
    }
}
