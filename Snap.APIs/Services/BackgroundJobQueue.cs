using System.Threading.Channels;

namespace Snap.APIs.Services
{
    /// <summary>
    /// Bounded channel (capacity 1000, full-mode = Wait).
    ///
    /// Why bounded + Wait:
    ///   Unbounded → RAM fills silently during traffic spikes.
    ///   DropWrite  → notifications are silently lost.
    ///   Wait       → producers yield asynchronously; natural back-pressure keeps
    ///                RAM bounded and surfaces overload at the HTTP layer where it
    ///                can be observed (slow response / 503 from upstream timeout).
    ///
    /// SingleReader = false because BackgroundJobProcessor spawns multiple workers.
    /// </summary>
    public sealed class BackgroundJobQueue : IBackgroundJobQueue
    {
        private const int Capacity = 1_000;

        private readonly Channel<Func<CancellationToken, Task>> _channel =
            Channel.CreateBounded<Func<CancellationToken, Task>>(
                new BoundedChannelOptions(Capacity)
                {
                    FullMode     = BoundedChannelFullMode.Wait,
                    SingleReader = false   // multi-worker processor
                });

        // Interlocked counter: incremented before write, decremented after read.
        // Represents items sitting in the channel waiting to be processed.
        private int _queueDepth;

        public int QueueDepth => _queueDepth;

        public async ValueTask EnqueueAsync(
            Func<CancellationToken, Task> workItem,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            // Increment BEFORE write so the counter is never under-reported
            Interlocked.Increment(ref _queueDepth);
            try
            {
                // WriteAsync yields (does not block a thread) when channel is full
                await _channel.Writer.WriteAsync(workItem, cancellationToken);
            }
            catch
            {
                // WriteAsync was cancelled or the channel was closed — roll back
                Interlocked.Decrement(ref _queueDepth);
                throw;
            }
        }

        public async ValueTask<Func<CancellationToken, Task>> DequeueAsync(
            CancellationToken cancellationToken)
        {
            var workItem = await _channel.Reader.ReadAsync(cancellationToken);
            // Decrement AFTER read — item leaves the "waiting" bucket
            Interlocked.Decrement(ref _queueDepth);
            return workItem;
        }
    }
}
