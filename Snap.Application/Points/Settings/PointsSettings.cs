namespace Snap.Application.Points.Settings
{
    /// <summary>
    /// Bound from appsettings.json → "PointsSettings". Mirrors the IOptions&lt;T&gt;
    /// binding pattern already used by Orders.Settings.OrderSettings.
    /// </summary>
    public sealed class PointsSettings
    {
        public const string SectionName = "PointsSettings";

        public int PointsPerCompletedOrder { get; set; } = 10;
    }
}
