namespace Snap.Application.Orders.Settings
{
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
