using Snap.Application.CarDatas.DTOs;

namespace Snap.Application.CarDatas.Interfaces
{
    public interface ICarDataService
    {
        Task<CarDataDto> CreateCarDataAsync(CarDataDto dto);
        Task<CarDataDto?> GetCarDataByDriverIdAsync(int driverId);
    }
}
