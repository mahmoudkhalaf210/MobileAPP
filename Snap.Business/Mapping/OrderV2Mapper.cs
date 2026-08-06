using Snap.Business.DTOs;
using Snap.Core.Entities;

namespace Snap.Business.Mapping
{
    public static class OrderV2Mapper
    {
        public static OrderV2Dto ToDto(Order o) => new()
        {
            Id            = o.Id,
            UserId        = o.UserId,
            Date          = o.Date,
            From          = o.From,
            To            = o.To,
            FromLatLng    = new LatLngV2Dto { Lat = o.FromLatLng.Lat, Lng = o.FromLatLng.Lng },
            ToLatLng      = new LatLngV2Dto { Lat = o.ToLatLng.Lat,   Lng = o.ToLatLng.Lng   },
            ExpectedPrice = o.ExpectedPrice,
            Type          = o.Type,
            Distance      = o.Distance,
            Notes         = o.Notes,
            NoPassengers  = o.NoPassengers,
            UserName      = o.UserName,
            UserPhone     = o.UserPhone,
            Status        = o.Status,
            DriverId      = o.Driverid,
            PaymentWay    = o.PaymentWay,
            // Orders created before the v2 rollout (or via the old endpoint) have no
            // typed CarType yet — default to Car rather than throwing.
            CarType       = o.CarTypeEnum ?? CarType.Car,
            PinkMode      = o.PinkMode
        };
    }
}
