namespace Snap.APIs.Settings
{
    /// <summary>
    /// Controls how new-order notifications are dispatched to drivers.
    /// Set via appsettings.json → "OrderSettings" : { "NotificationMode": "..." }
    /// </summary>
    public enum DriverNotificationMode
    {
        /// <summary>
        /// Notify only the nearest <c>NearestDriverCount</c> available online drivers.
        /// Uses bounding-box pre-filter + min-heap — best for production.
        /// </summary>
        NearestOnly,

        /// <summary>
        /// Notify every driver in the system regardless of location.
        /// Useful during early rollout when driver density is low.
        /// </summary>
        AllDrivers
    }

    public sealed class OrderSettings
    {
        public const string SectionName = "OrderSettings";

        /// <summary>Switches between targeted and broadcast notification.</summary>
        public DriverNotificationMode NotificationMode { get; set; } = DriverNotificationMode.NearestOnly;

        /// <summary>
        /// Maximum drivers to notify when <see cref="NotificationMode"/> is
        /// <see cref="DriverNotificationMode.NearestOnly"/>. Ignored for AllDrivers.
        /// </summary>
        public int NearestDriverCount { get; set; } = 10;

        public int ScheduledDispatchLeadTimeMinutes { get; set; } = 10;
        public int ScheduledCancelCutoffMinutes { get; set; } = 15;

        public int ScheduledReminderMinutes { get; set; } = 60;
        public int ScheduledStartingSoonMinutes { get; set; } = 10;
        public int ScheduledConflictWindowMinutes { get; set; } = 60;
    }
}
