namespace Snap.Application.Points.Interfaces
{
    public interface IDriverPointsService
    {
        Task<int> GetBalanceAsync(int driverId);

        /// <summary>Additive, idempotent — safe to call more than once for the same order.</summary>
        Task AwardForCompletedOrderAsync(int orderId, int driverId);
    }
}
