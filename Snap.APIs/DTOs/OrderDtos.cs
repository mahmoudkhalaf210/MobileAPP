using Microsoft.Win32;
using System;
using System.Text.Json.Serialization;

namespace Snap.APIs.DTOs
{
    public class LatLngDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public DateTime Date { get; set; }
        public string From { get; set; } = null!;
        public string To { get; set; } = null!;
        public LatLngDto FromLatLng { get; set; } = new LatLngDto();
        public LatLngDto ToLatLng { get; set; } = new LatLngDto();
        public double ExpectedPrice { get; set; }
        public string Type { get; set; } = null!;
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

    public class CreateOrderDto
    {
        public string UserId { get; set; } = null!;
        public DateTime Date { get; set; }
        public string From { get; set; } = null!;
        public string To { get; set; } = null!;
        public LatLngDto FromLatLng { get; set; } = new LatLngDto();
        public LatLngDto ToLatLng { get; set; } = new LatLngDto();
        public double ExpectedPrice { get; set; }
        public string Type { get; set; } = null!;
        public double Distance { get; set; }
        public string? Notes { get; set; }
        public int NoPassengers { get; set; }
        public string? PaymentWay { get; set; }
        public string CarType { get; set; }
        public bool PinkMode { get; set; }
        public string? FCMToken { get; set; }

    }

    public class UpdateOrderStatusDto
    {
        public int OrderId { get; set; }
        public int? Driverid { get; set; }
        public string? FCMToken { get; set; }
    }

    public class UpdateOrderStatusResponseDto
    {
        public int OrderId { get; set; }
        public string? FCMToken { get; set; }
    }

    public class UpdateOrderDriverDto
    {
        public int OrderId { get; set; }
        public int Driverid { get; set; }
        public string Status { get; set; } = null!;
        public string? FCMToken { get; set; }
    }

    public class TestNotificationDto
    {
        public string Token { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
    }

    public class PublicUserInfoDto
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Image { get; set; }
        public string? Gender { get; set; }
    }

    public class PublicDriverInfoDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
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

    public class TripDetailsDto
    {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public string From { get; set; } = null!;
        public string To { get; set; } = null!;
        public LatLngDto FromLatLng { get; set; } = new LatLngDto();
        public LatLngDto ToLatLng { get; set; } = new LatLngDto();
        public double ExpectedPrice { get; set; }
        public double Budget { get; set; }
        public double Fee { get; set; }
        public string Type { get; set; } = null!;
        public double Distance { get; set; }
        public string? Notes { get; set; }
        public int NoPassengers { get; set; }
        public string? PaymentWay { get; set; }
        public string CarType { get; set; } = null!;
        public bool PinkMode { get; set; }
        public string? Status { get; set; }
        public double Review { get; set; }
    }

    public class UserHistoryDetailsDto
    {
        public PublicUserInfoDto User { get; set; } = new PublicUserInfoDto();
        public PublicDriverInfoDto? Driver { get; set; }
        public TripDetailsDto Trip { get; set; } = new TripDetailsDto();
    }

    public class DriverTripHistoryDetailsDto
    {
        public PublicUserInfoDto User { get; set; } = new PublicUserInfoDto();
        public PublicDriverInfoDto Driver { get; set; } = new PublicDriverInfoDto();
        public TripDetailsDto Trip { get; set; } = new TripDetailsDto();
    }

    public class CancelOrderByUserDto
    {
        public int OrderId { get; set; }
        public string UserId { get; set; } = null!;
    }
}
