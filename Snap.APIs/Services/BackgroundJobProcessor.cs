namespace Snap.APIs.Services
{
    /// <summary>
    /// Drains <see cref="IBackgroundJobQueue"/> using N parallel workers.
    ///
    /// Worker count = clamp(ProcessorCount, 2, 4).
    ///   • 1 worker  → sequential; 100 jobs × 500 ms = 50 s latency tail.
    ///   • 2-4 workers → jobs overlap; same 100 jobs finish in ~12-25 s.
    ///   • > 4        → FCM and the DB become the bottleneck anyway.
    ///
    /// Each worker catches its own exceptions so one failing job never kills
    /// a worker loop.  ExecuteAsync itself only exits on graceful shutdown.
    /// </summary>
    public sealed class BackgroundJobProcessor : BackgroundService
    {
        private readonly IBackgroundJobQueue         _jobQueue;
        private readonly ILogger<BackgroundJobProcessor> _logger;

        public BackgroundJobProcessor(
            IBackgroundJobQueue jobQueue,
            ILogger<BackgroundJobProcessor> logger)
        {
            _jobQueue = jobQueue;
            _logger   = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workerCount = Math.Clamp(Environment.ProcessorCount, 2, 4);
            _logger.LogInformation("BackgroundJobProcessor starting {Workers} workers", workerCount);

            var workers = Enumerable
                .Range(0, workerCount)
                .Select(i => RunWorkerAsync(i, stoppingToken));

            // WhenAll keeps ExecuteAsync alive until all workers exit (shutdown)
            return Task.WhenAll(workers);
        }

        private async Task RunWorkerAsync(int workerId, CancellationToken ct)
        {
            _logger.LogDebug("BackgroundJobProcessor worker {Id} started", workerId);

            while (!ct.IsCancellationRequested)
            {
                Func<CancellationToken, Task> workItem;

                try
                {
                    workItem = await _jobQueue.DequeueAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break; // graceful shutdown signal
                }

                try
                {
                    await workItem(ct);
                }
                catch (Exception ex)
                {
                    // Work items catch their own domain errors; this guard keeps
                    // the worker loop alive after truly unexpected failures.
                    _logger.LogError(ex, "Worker {Id}: unhandled exception in background job", workerId);
                }
            }

            _logger.LogDebug("BackgroundJobProcessor worker {Id} stopped", workerId);
        }
    }
}
