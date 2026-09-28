namespace Snap.Application.Points.Settings
{
    /// <summary>
    /// Bound from appsettings.json → "DriverPointsSettings". Mirrors PointsSettings
    /// (the rider-points equivalent) but kept separate so the per-order award amount
    /// can differ between riders and drivers.
    /// </summary>
    public sealed class DriverPointsSettings
    {
        public const string SectionName = "DriverPointsSettings";

        public int PointsPerCompletedOrder { get; set; } = 10;
    }
}
