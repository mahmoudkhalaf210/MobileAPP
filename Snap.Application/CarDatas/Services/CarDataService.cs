using Snap.Application.CarDatas.DTOs;
using Snap.Application.CarDatas.Interfaces;
using Snap.Application.Common.Interfaces.Repositories;

namespace Snap.Application.CarDatas.Services
{
    public class CarDataService : ICarDataService
    {
        private readonly IRepository<Domain.Entities.CarData> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public CarDataService(IRepository<Domain.Entities.CarData> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<CarDataDto> CreateCarDataAsync(CarDataDto dto)
        {
            var carData = new Domain.Entities.CarData
            {
                CarPhoto = dto.CarPhoto,
                LicenseFront = dto.LicenseFront,
                LicenseBack = dto.LicenseBack,
                CarBrand = dto.CarBrand,
                CarModel = dto.CarModel,
                CarColor = dto.CarColor,
                PlateNumber = dto.PlateNumber,
                DriverId = dto.DriverId
            };

            await _repo.AddAsync(carData);
            await _unitOfWork.SaveChangesAsync();

            dto.Id = carData.Id;
            return dto;
        }

        public async Task<CarDataDto?> GetCarDataByDriverIdAsync(int driverId)
        {
            var carData = (await _repo.FindAsync(c => c.DriverId == driverId)).FirstOrDefault();
            if (carData == null) return null;

            return new CarDataDto
            {
                Id = carData.Id,
                CarPhoto = carData.CarPhoto,
                LicenseFront = carData.LicenseFront,
                LicenseBack = carData.LicenseBack,
                CarBrand = carData.CarBrand,
                CarModel = carData.CarModel,
                CarColor = carData.CarColor,
                PlateNumber = carData.PlateNumber,
                DriverId = carData.DriverId
            };
        }
    }
}
