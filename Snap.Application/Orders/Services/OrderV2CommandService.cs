using Snap.Application.Common.DTOs;
using Snap.Application.Domain.Enums;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;

namespace Snap.Application.Orders.Services
{
    /// <summary>
    /// Composition adapter: v2 order creation reuses the existing, untouched
    /// <see cref="IOrderService.CreateOrderAsync"/> (persistence, driver matching,
    /// FCM/WebSocket dispatch via the background job queue) instead of duplicating
    /// that logic. It only adds the typed <see cref="CarType"/> as a follow-up,
    /// single-column write via <see cref="IOrderRepositoryV2.SetCarTypeEnumAsync"/>.
    /// </summary>
    public sealed class OrderV2CommandService : IOrderV2CommandService
    {
        private readonly IOrderService _legacyOrderService;
        private readonly IOrderRepositoryV2 _orderRepoV2;

        public OrderV2CommandService(IOrderService legacyOrderService, IOrderRepositoryV2 orderRepoV2)
        {
            _legacyOrderService = legacyOrderService;
            _orderRepoV2 = orderRepoV2;
        }

        public async Task<OrderV2Dto> CreateNormalAsync(CreateNormalOrderV2Dto dto)
        {
            // Date = now forces the legacy isScheduled branch in OrderService.CreateOrderAsync to false.
            var legacyDto = MapToLegacy(dto, DateTime.UtcNow);
            var created = await _legacyOrderService.CreateOrderAsync(legacyDto);
            await _orderRepoV2.SetCarTypeEnumAsync(created.Id, dto.CarType);
            return ToV2Dto(created, dto.CarType);
        }

        public async Task<OrderV2Dto> CreateScheduledAsync(CreateScheduledOrderV2Dto dto)
        {
            // dto.Date is preserved so the legacy isScheduled branch fires naturally.
            var legacyDto = MapToLegacy(dto, dto.Date);
            var created = await _legacyOrderService.CreateOrderAsync(legacyDto);
            await _orderRepoV2.SetCarTypeEnumAsync(created.Id, dto.CarType);
            return ToV2Dto(created, dto.CarType);
        }

        private static CreateOrderDto MapToLegacy(CreateNormalOrderV2Dto dto, DateTime date) => new()
        {
            UserId        = dto.UserId,
            Date          = date,
            From          = dto.From,
            To            = dto.To,
            FromLatLng    = new LatLngDto { Lat = dto.FromLatLng.Lat, Lng = dto.FromLatLng.Lng },
            ToLatLng      = new LatLngDto { Lat = dto.ToLatLng.Lat,   Lng = dto.ToLatLng.Lng   },
            ExpectedPrice = dto.ExpectedPrice,
            Type          = dto.Type,
            Distance      = dto.Distance,
            Notes         = dto.Notes,
            NoPassengers  = dto.NoPassengers,
            PaymentWay    = dto.PaymentWay,
            // Legacy CarType column is NOT NULL — kept populated for full backward compatibility.
            CarType       = dto.CarType.ToString(),
            PinkMode      = dto.PinkMode,
            FCMToken      = dto.FCMToken
        };

        private static OrderV2Dto ToV2Dto(OrderDto created, CarType carType) => new()
        {
            Id            = created.Id,
            UserId        = created.UserId,
            Date          = created.Date,
            From          = created.From,
            To            = created.To,
            FromLatLng    = new LatLngV2Dto { Lat = created.FromLatLng.Lat, Lng = created.FromLatLng.Lng },
            ToLatLng      = new LatLngV2Dto { Lat = created.ToLatLng.Lat,   Lng = created.ToLatLng.Lng   },
            ExpectedPrice = created.ExpectedPrice,
            Type          = created.Type,
            Distance      = created.Distance,
            Notes         = created.Notes,
            NoPassengers  = created.NoPassengers,
            UserName      = created.UserName,
            UserPhone     = created.UserPhone,
            Status        = created.Status,
            DriverId      = created.Driverid,
            PaymentWay    = created.PaymentWay,
            CarType       = carType,
            PinkMode      = created.PinkMode
        };
    }
}
