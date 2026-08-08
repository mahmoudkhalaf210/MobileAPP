namespace Snap.Application.AppVersions.DTOs
{
    public class AppVersionCheckResultDto
    {
        public string LatestVersion { get; set; }
        public string MinSupportedVersion { get; set; }
        public bool ForceUpdate { get; set; }
        public string UpdateUrl { get; set; }
        public string Message { get; set; }
    }
}
