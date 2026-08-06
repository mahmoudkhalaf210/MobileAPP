namespace Snap.Business.Interfaces
{
    public interface IPointsService
    {
        Task<int> GetBalanceAsync(string userId);

        /// <summary>Additive, idempotent — safe to call more than once for the same order.</summary>
        Task AwardForCompletedOrderAsync(int orderId, string userId);
    }
}
