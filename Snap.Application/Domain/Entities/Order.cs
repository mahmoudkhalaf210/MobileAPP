using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Snap.Application.Domain.Enums;
using Snap.Application.Domain.ValueObjects;

namespace Snap.Application.Domain.Entities
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        // Link to user (AspNetUsers)
        [Required]
        public string UserId { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        public DateTime Date { get; set; }

        [Required]
        public string From { get; set; } = null!;

        [Required]
        public string To { get; set; } = null!;

        public LatLng FromLatLng { get; set; } = new LatLng();
        public LatLng ToLatLng { get; set; } = new LatLng();

        public double ExpectedPrice { get; set; }

        // ride | delivery
        [Required]
        public string Type { get; set; } = null!;

        public double Distance { get; set; }

        public string? Notes { get; set; }

        public int NoPassengers { get; set; }
        public string? UserImage { get; set; }
        public string? UserName { get; set; }
        public string? UserPhone { get; set; }
        public string? Status { get; set; } = "pending";
        public int? Driverid { get; set; }
        public double Review { get; set; }
        public string? PaymentWay { get; set; }
        public string CarType { get; set; }
        public bool PinkMode { get; set; }
        public string? FCMToken { get; set; }

        // v2 addition — additive, nullable. Legacy string CarType above is untouched
        // and remains the source of truth for all existing (v1) code paths.
        public CarType? CarTypeEnum { get; set; }

        // Set when the user cancels the order via CancelOrderByUserAsync, from the
        // reason they selected (Orders/DTOs/CancelReasonDto). Null for driver
        // cancellations or orders never cancelled.
        public int? CancelReasonId { get; set; }
    }
}
