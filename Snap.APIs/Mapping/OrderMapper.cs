using System.Linq.Expressions;
using Snap.APIs.DTOs;
using Snap.Core.Entities;

namespace Snap.APIs.Mapping
{
    /// <summary>
    /// Single place for Order → OrderDto conversion.
    ///
    /// <see cref="ToProjection"/> is an expression tree usable inside EF Core
    /// LINQ queries (.Select), so the mapping runs as SQL projection — no
    /// full entity materialisation.
    ///
    /// <see cref="ToDto"/> is used when the Order is already loaded in memory
    /// (e.g., after a FindAsync + SaveChanges cycle).
    /// </summary>
    public static class OrderMapper
    {
        public static readonly Expression<Func<Order, OrderDto>> ToProjection = o => new OrderDto
        {
            Id            = o.Id,
            UserId        = o.UserId,
            Date          = o.Date,
            From          = o.From,
            To            = o.To,
            FromLatLng    = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
            ToLatLng      = new LatLngDto { Lat = o.ToLatLng.Lat,   Lng = o.ToLatLng.Lng   },
            ExpectedPrice = o.ExpectedPrice,
            Type          = o.Type,
            Distance      = o.Distance,
            Notes         = o.Notes,
            NoPassengers  = o.NoPassengers,
            UserImage     = o.UserImage,
            UserName      = o.UserName,
            UserPhone     = o.UserPhone,
            Status        = o.Status,
            Driverid      = o.Driverid,
            Review        = o.Review,
            PaymentWay    = o.PaymentWay,
            CarType       = o.CarType,
            PinkMode      = o.PinkMode,
            FCMToken      = o.FCMToken
        };

        public static OrderDto ToDto(Order o) => new()
        {
            Id            = o.Id,
            UserId        = o.UserId,
            Date          = o.Date,
            From          = o.From,
            To            = o.To,
            FromLatLng    = new LatLngDto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
            ToLatLng      = new LatLngDto { Lat = o.ToLatLng.Lat,   Lng = o.ToLatLng.Lng   },
            ExpectedPrice = o.ExpectedPrice,
            Type          = o.Type,
            Distance      = o.Distance,
            Notes         = o.Notes,
            NoPassengers  = o.NoPassengers,
            UserImage     = o.UserImage,
            UserName      = o.UserName,
            UserPhone     = o.UserPhone,
            Status        = o.Status,
            Driverid      = o.Driverid,
            Review        = o.Review,
            PaymentWay    = o.PaymentWay,
            CarType       = o.CarType,
            PinkMode      = o.PinkMode,
            FCMToken      = o.FCMToken
        };
    }
}
