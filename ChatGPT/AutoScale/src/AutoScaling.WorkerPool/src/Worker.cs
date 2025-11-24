using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace AutoScaling.WorkerPool
{
    internal class Worker
    {
        private readonly string _id;
        private readonly Func<CancellationToken, Task<ScheduledWorkItem?>> _fetcher;
        private readonly IWorkerPoolMetrics? _metrics;
        private CancellationTokenSource? _cts;
        private Task? _loop;

        public DateTime LastActiveUtc { get; private set; } = DateTime.UtcNow;

        public string Id => _id;

        public Worker(Func<CancellationToken, Task<ScheduledWorkItem?>> fetcher, IWorkerPoolMetrics? metrics = null)
        {
            _id = Guid.NewGuid().ToString("D");
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
            _metrics = metrics;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
            _metrics?.WorkerCreated(_id);
        }

        public async Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
                if (_loop != null)
                {
                    await _loop.ConfigureAwait(false);
                }
            }
            finally
            {
                _metrics?.WorkerDestroyed(_id);
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var item = await _fetcher(cancellationToken).ConfigureAwait(false);
                if (item is null)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                LastActiveUtc = DateTime.UtcNow;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool succeeded = true;
                try
                {
                    await item.Work(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    succeeded = false;
                }
                catch
                {
                    succeeded = false;
                    // swallow; metrics will report
                }
                finally
                {
                    sw.Stop();
                    _metrics?.TaskCompleted(item, sw.Elapsed, succeeded);
                }
            }
        }
    }
}
