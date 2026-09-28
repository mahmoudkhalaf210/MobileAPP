using Snap.Application.CarDatas.DTOs;
using Snap.Application.Domain.Enums;

namespace Snap.Application.CarDatas.Interfaces
{
    public interface ICarDataService
    {
        Task<CarDataDto> CreateCarDataAsync(CarDataDto dto);
        Task<CarDataDto?> GetCarDataByDriverIdAsync(int driverId);
        Task<CarDataDto> UpdateCarTypeAsync(int driverId, CarType carType);
    }
}
