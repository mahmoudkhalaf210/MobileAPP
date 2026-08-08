using System;
using Snap.Application.Common.DTOs;

namespace Snap.Application.Orders.DTOs
{
    public class OrderDto
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public DateTime Date { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public LatLngDto FromLatLng { get; set; }
        public LatLngDto ToLatLng { get; set; }
        public double ExpectedPrice { get; set; }
        public string Type { get; set; }
        public double Distance { get; set; }
        public string? Notes { get; set; }
        public int NoPassengers { get; set; }
        public string? UserImage { get; set; }
        public string? UserName { get; set; }
        public string? UserPhone { get; set; }
        public string? Status { get; set; }
        public int? Driverid { get; set; }
        public double Review { get; set; }
        public string? PaymentWay { get; set; }
        public string CarType { get; set; }
        public bool PinkMode { get; set; }
        public string? FCMToken { get; set; }
    }
}
