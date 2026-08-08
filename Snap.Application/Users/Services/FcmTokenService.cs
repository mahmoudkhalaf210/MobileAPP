using Snap.Application.Common.Interfaces.Repositories;
using Snap.Application.Domain.Entities;
using Snap.Application.Users.Interfaces;

namespace Snap.Application.Users.Services
{
    public class FcmTokenService : IFcmTokenService
    {
        private readonly IRepository<FCMTokenUser> _repo;
        private readonly IUnitOfWork _unitOfWork;

        public FcmTokenService(IRepository<FCMTokenUser> repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task SaveTokenAsync(string userId, string token)
        {
            var existingToken = (await _repo.FindAsync(t => t.UserId == userId)).FirstOrDefault();
            if (existingToken != null)
            {
                existingToken.Token = token;
                _repo.Update(existingToken);
            }
            else
            {
                await _repo.AddAsync(new FCMTokenUser
                {
                    UserId = userId,
                    Token = token
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
