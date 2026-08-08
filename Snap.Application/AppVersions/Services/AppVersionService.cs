using Snap.Application.AppVersions.DTOs;
using Snap.Application.AppVersions.Interfaces;
using Snap.Application.Common.Interfaces.Repositories;

namespace Snap.Application.AppVersions.Services
{
    public class AppVersionService : IAppVersionService
    {
        private readonly IRepository<Domain.Entities.AppVersion> _repo;

        public AppVersionService(IRepository<Domain.Entities.AppVersion> repo)
        {
            _repo = repo;
        }

        public async Task<AppVersionCheckResultDto?> CheckVersionAsync(string currentVersion, string platform)
        {
            var version = (await _repo.FindAsync(x => x.Platform == platform)).FirstOrDefault();

            if (version == null)
                return null;

            var userVersion = new Version(currentVersion);
            var minVersion = new Version(version.MinSupportedVersion);

            bool forceUpdate = userVersion < minVersion;

            return new AppVersionCheckResultDto
            {
                LatestVersion = version.LatestVersion,
                MinSupportedVersion = version.MinSupportedVersion,
                ForceUpdate = forceUpdate,
                UpdateUrl = version.UpdateUrl,
                Message = version.Message
            };
        }
    }
}
