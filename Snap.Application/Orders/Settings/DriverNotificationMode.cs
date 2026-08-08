namespace Snap.Application.Orders.Settings
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
}
