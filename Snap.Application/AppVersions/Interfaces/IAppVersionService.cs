using Snap.Application.AppVersions.DTOs;

namespace Snap.Application.AppVersions.Interfaces
{
    public interface IAppVersionService
    {
        Task<AppVersionCheckResultDto?> CheckVersionAsync(string currentVersion, string platform);
    }
}
