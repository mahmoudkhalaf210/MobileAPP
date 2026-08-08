using System;

namespace Snap.Application.Common.DTOs
{
    public class TripDetailsDto
    {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public LatLngDto FromLatLng { get; set; }
        public LatLngDto ToLatLng { get; set; }
        public double ExpectedPrice { get; set; }
        public double Budget { get; set; }
        public double Fee { get; set; }
        public string Type { get; set; }
        public double Distance { get; set; }
        public string? Notes { get; set; }
        public int NoPassengers { get; set; }
        public string? PaymentWay { get; set; }
        public string CarType { get; set; }
        public bool PinkMode { get; set; }
        public string? Status { get; set; }
        public double Review { get; set; }
    }
}
