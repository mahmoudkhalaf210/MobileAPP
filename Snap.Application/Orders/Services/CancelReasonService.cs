using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Domain.Entities;
using Snap.Application.Orders.DTOs;
using Snap.Application.Orders.Interfaces;

namespace Snap.Application.Orders.Services
{
    public class CancelReasonService : ICancelReasonService
    {
        private readonly IRepository<CancelReason> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public CancelReasonService(IRepository<CancelReason> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task<CancelReasonDto> CreateAsync(CreateCancelReasonDto dto)
        {
            var reason = new CancelReason
            {
                TextEn = dto.TextEn,
                TextAr = dto.TextAr
            };

            await _repo.AddAsync(reason);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(reason);
        }

        public async Task<List<CancelReasonDto>> GetAllAsync() =>
            (await _repo.GetAllAsync()).Select(ToDto).ToList();

        private static CancelReasonDto ToDto(CancelReason r) => new()
        {
            Id = r.Id,
            TextEn = r.TextEn,
            TextAr = r.TextAr
        };
    }
}
