namespace Snap.Application.Points.Interfaces
{
    public interface IDriverPointsRepository
    {
        Task<int> GetBalanceAsync(int driverId);

        /// <summary>
        /// Idempotent: awarding points for an OrderId that already has a ledger
        /// entry is a no-op. Returns true only when points were actually credited.
        /// </summary>
        Task<bool> TryAwardPointsForOrderAsync(int orderId, int driverId, int points);
    }
}
