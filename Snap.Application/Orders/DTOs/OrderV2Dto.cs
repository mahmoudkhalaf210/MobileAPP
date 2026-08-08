using System;
using Snap.Application.Domain.Enums;

namespace Snap.Application.Orders.DTOs
{
    public class OrderV2Dto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public DateTime Date { get; set; }
        public string From { get; set; } = null!;
        public string To { get; set; } = null!;
        public LatLngV2Dto FromLatLng { get; set; } = new();
        public LatLngV2Dto ToLatLng { get; set; } = new();
        public double ExpectedPrice { get; set; }
        public string Type { get; set; } = null!;
        public double Distance { get; set; }
        public string? Notes { get; set; }
        public int NoPassengers { get; set; }
        public string? UserName { get; set; }
        public string? UserPhone { get; set; }
        public string? Status { get; set; }
        public int? DriverId { get; set; }
        public string? PaymentWay { get; set; }
        public CarType CarType { get; set; }
        public bool PinkMode { get; set; }
    }
}
