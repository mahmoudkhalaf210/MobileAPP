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
            // An FCM token identifies one app install (one phone). If that phone was
            // previously logged in as another account (e.g. a driver account, then a
            // customer account), the old account still points at this phone and it would
            // keep receiving that account's pushes — new-order pushes on a customer's
            // phone, or another car type's orders. The token belongs only to the account
            // that registered it last.
            if (!string.IsNullOrWhiteSpace(token))
            {
                foreach (var stale in await _repo.FindAsync(t => t.Token == token && t.UserId != userId))
                    _repo.Remove(stale);
            }

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
