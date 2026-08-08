using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Domain.Entities;
using Snap.Application.Drivers.DTOs;
using Snap.Application.Drivers.Interfaces;
using Snap.Application.Orders.Interfaces;

namespace Snap.Application.Drivers.Services
{
    public class DriverService : IDriverService
    {
        private readonly IDriverRepository _driverRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly IUnitOfWork _unitOfWork;

        public DriverService(IDriverRepository driverRepo, IOrderRepository orderRepo, IUnitOfWork unitOfWork)
        {
            _driverRepo = driverRepo;
            _orderRepo = orderRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateDriver> CreateDriverAsync(CreateDriver dto)
        {
            var driver = new Driver
            {
                DriverPhoto = dto.DriverPhoto,
                DriverIdCard = dto.DriverIdCard,
                DriverLicenseFront = dto.DriverLicenseFront,
                DriverLicenseBack = dto.DriverLicenseBack,
                IdCardFront = dto.IdCardFront,
                IdCardBack = dto.IdCardBack,
                DriverFullname = dto.DriverFullname,
                NationalId = dto.NationalId,
                Age = dto.Age,
                LicenseNumber = dto.LicenseNumber,
                Email = dto.Email,
                Password = dto.Password,
                LicenseExpiryDate = dto.LicenseExpiryDate,
                UserId = dto.UserId,
                Status = "pending",
            };

            _driverRepo.Add(driver);
            await _unitOfWork.SaveChangesAsync();

            dto.Id = driver.Id;
            return dto;
        }

        public async Task<DriverDto> GetDriverByUserIdAsync(string userId)
        {
            var driver = await _driverRepo.GetByUserIdAsync(userId)
                ?? throw new KeyNotFoundException("Driver not found");

            double avg = 0;
            if (driver.NoReviews > 0)
                avg = (double)driver.TotalReview / driver.NoReviews;
            if (avg > 5) avg = 5;

            var user = await _driverRepo.GetUserByIdAsync(driver.UserId);
            string gender = user?.Gender;
            string phoneNumber = user?.PhoneNumber;

            var carData = await _driverRepo.GetCarDataByDriverIdAsync(driver.Id);
            string carBrand = carData?.CarBrand;

            return new DriverDto
            {
                Id = driver.Id,
                DriverPhoto = driver.DriverPhoto,
                DriverIdCard = driver.DriverIdCard,
                DriverLicenseFront = driver.DriverLicenseFront,
                DriverLicenseBack = driver.DriverLicenseBack,
                IdCardFront = driver.IdCardFront,
                IdCardBack = driver.IdCardBack,
                DriverFullname = driver.DriverFullname,
                NationalId = driver.NationalId,
                Age = driver.Age,
                LicenseNumber = driver.LicenseNumber,
                Email = driver.Email,
                Password = driver.Password,
                LicenseExpiryDate = driver.LicenseExpiryDate,
                UserId = driver.UserId,
                Status = driver.Status,
                Review = avg,
                Wallet = driver.Wallet,
                Gender = gender,
                CarBrand = carBrand,
                PhoneNumber = phoneNumber,
            };
        }

        public async Task<List<PendingDto>> GetPendingDriversAsync()
        {
            var pending = await _driverRepo.GetPendingAsync();
            return pending.Select(d => new PendingDto
            {
                id = d.Id,
                driverFullname = d.DriverFullname,
                email = d.Email,
                status = d.Status.ToString(),
                userId = d.UserId
            }).ToList();
        }

        public async Task ChangeDriverStatusAsync(int driverId, ChangeDriverStatusDto dto)
        {
            var driver = await _driverRepo.GetTrackedByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Driver not found");

            var allowed = new[] { "pending", "approved", "reject" };
            if (string.IsNullOrWhiteSpace(dto.Status) || !allowed.Contains(dto.Status.ToLower()))
                throw new ArgumentException("Invalid status value. Allowed: pending, approved, reject.");

            driver.Status = dto.Status.ToLower();
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task AddReviewAsync(int id, AddDriverReviewDto dto)
        {
            if (dto.Review < 0 || dto.Review > 5)
                throw new ArgumentException("Review must be between 0 and 5.");

            var driver = await _driverRepo.GetTrackedByIdAsync(id)
                ?? throw new KeyNotFoundException("Driver not found");

            driver.TotalReview += dto.Review;
            driver.NoReviews += 1;

            if (dto.OrderId != null)
            {
                var order = await _orderRepo.FindTrackedAsync(dto.OrderId.Value);
                if (order != null)
                {
                    order.Review = dto.Review;
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<double> GetDriverReviewAsync(int id)
        {
            var driver = await _driverRepo.GetTrackedByIdAsync(id)
                ?? throw new KeyNotFoundException("Driver not found");

            if (driver.NoReviews == 0) return 0;

            var avg = (double)driver.TotalReview / driver.NoReviews;
            if (avg > 5) avg = 5;
            return avg;
        }

        public async Task<Charge> RequestChargeAsync(RequestChargeDto dto)
        {
            var driver = await _driverRepo.GetTrackedByIdAsync(dto.DriverId)
                ?? throw new KeyNotFoundException("Driver not found");

            var charge = new Charge
            {
                DriverId = dto.DriverId,
                Name = dto.Name,
                Image = dto.Image
            };

            _driverRepo.AddCharge(charge);
            await _unitOfWork.SaveChangesAsync();
            return charge;
        }

        public Task<List<Charge>> GetChargesAsync() => _driverRepo.GetChargesWithDriverAsync();

        public async Task<string> HandleChargeAsync(int id, ChargeActionDto dto)
        {
            var charge = await _driverRepo.GetChargeWithDriverAsync(id)
                ?? throw new KeyNotFoundException("Charge not found");

            if (dto.Action.ToLower() == "approve")
            {
                charge.Driver.Wallet += dto.value;
                _driverRepo.RemoveCharge(charge);
                await _unitOfWork.SaveChangesAsync();
                return "Charge approved and wallet updated.";
            }
            else if (dto.Action.ToLower() == "reject")
            {
                _driverRepo.RemoveCharge(charge);
                await _unitOfWork.SaveChangesAsync();
                return "Charge rejected and removed.";
            }
            else
            {
                throw new ArgumentException("Invalid action. Use 'approve' or 'reject'.");
            }
        }

        public async Task<double> DeductFromWalletAsync(DeductWalletDto dto)
        {
            if (dto.Amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.");

            var driver = await _driverRepo.GetTrackedByIdAsync(dto.DriverId)
                ?? throw new KeyNotFoundException("Driver not found");

            if (driver.Wallet < dto.Amount)
                throw new ArgumentException("Insufficient wallet balance.");

            driver.Wallet -= dto.Amount;
            await _unitOfWork.SaveChangesAsync();
            return driver.Wallet;
        }

        public async Task<DriverIdDto> GetDriverByDriverIdAsync(int id)
        {
            var driver = await _driverRepo.GetTrackedByIdAsync(id)
                ?? throw new KeyNotFoundException("Driver not found");

            double avg = 0;
            if (driver.NoReviews > 0)
                avg = (double)driver.TotalReview / driver.NoReviews;
            if (avg > 5) avg = 5;

            var user = await _driverRepo.GetUserByIdAsync(driver.UserId);
            string phoneNumber = user?.PhoneNumber;
            string gender = user?.Gender;

            var carData = await _driverRepo.GetCarDataByDriverIdAsync(driver.Id);
            string carBrand = carData?.CarBrand;

            return new DriverIdDto
            {
                Id = driver.Id,
                DriverPhoto = driver.DriverPhoto,
                DriverFullname = driver.DriverFullname,
                Email = driver.Email,
                UserId = driver.UserId,
                Review = avg,
                PhoneNumber = phoneNumber != null ? int.Parse(phoneNumber) : (int?)null,
                Gender = gender,
                CarBrand = carBrand
            };
        }

        public async Task<List<ApprovedDriverWithCarDto>> GetApprovedDriversAsync()
        {
            var approvedDrivers = await _driverRepo.GetApprovedWithDetailsAsync();

            return approvedDrivers.Select(item =>
            {
                double avg = 0;
                if (item.Driver.NoReviews > 0)
                    avg = (double)item.Driver.TotalReview / item.Driver.NoReviews;
                if (avg > 5) avg = 5;

                return new ApprovedDriverWithCarDto
                {
                    DriverId = item.Driver.Id,
                    DriverPhoto = item.Driver.DriverPhoto,
                    DriverFullname = item.Driver.DriverFullname,
                    Email = item.Driver.Email,
                    Age = item.Driver.Age,
                    LicenseNumber = item.Driver.LicenseNumber,
                    LicenseExpiryDate = item.Driver.LicenseExpiryDate,
                    UserId = item.Driver.UserId,
                    Review = avg,
                    Wallet = item.Driver.Wallet,
                    Gender = item.User?.Gender,
                    PhoneNumber = item.User?.PhoneNumber,
                    CarId = item.CarData?.Id,
                    CarPhoto = item.CarData?.CarPhoto,
                    CarLicenseFront = item.CarData?.LicenseFront,
                    CarLicenseBack = item.CarData?.LicenseBack,
                    CarBrand = item.CarData?.CarBrand,
                    CarModel = item.CarData?.CarModel,
                    CarColor = item.CarData?.CarColor,
                    PlateNumber = item.CarData?.PlateNumber
                };
            }).ToList();
        }
    }
}
