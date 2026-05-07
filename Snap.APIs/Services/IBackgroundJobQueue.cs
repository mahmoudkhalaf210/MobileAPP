namespace Snap.APIs.Services
{
    /// <summary>
    /// Bounded, in-process background job queue backed by System.Threading.Channels.
    /// Producers await <see cref="EnqueueAsync"/> — if the channel is full they yield
    /// (not block a thread) until a worker drains space.  Never fire-and-forget.
    /// </summary>
    public interface IBackgroundJobQueue
    {
        /// <summary>Number of jobs waiting in the channel right now.</summary>
        int QueueDepth { get; }

        /// <summary>
        /// Enqueues a work item.  Awaits asynchronously if the channel is at capacity
        /// (back-pressure); the calling HTTP thread is released while waiting.
        /// </summary>
        ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem,
                               CancellationToken cancellationToken = default);

        /// <summary>Dequeues the next item, waiting until one arrives.</summary>
        ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }
}
