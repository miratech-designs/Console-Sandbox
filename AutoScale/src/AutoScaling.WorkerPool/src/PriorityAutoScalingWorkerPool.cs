using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AutoScaling.WorkerPool
{
    public class PriorityAutoScalingWorkerPool : IWorkerPool
    {
        private readonly WorkerPoolConfig _config;
        private readonly IWorkerPoolMetrics? _metrics;
        private readonly Channel<ScheduledWorkItem>[] _channels;
        private readonly ConcurrentDictionary<string, Worker> _workers = new();
        private readonly ConcurrentDictionary<WorkPriority, int> _backlog = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _managementLoop;
        private readonly object _sync = new();

        public PriorityAutoScalingWorkerPool(WorkerPoolConfig config, IWorkerPoolMetrics? metrics = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _metrics = metrics;
            _channels = new[] {
                Channel.CreateUnbounded<ScheduledWorkItem>(), // High
                Channel.CreateUnbounded<ScheduledWorkItem>(), // Normal
                Channel.CreateUnbounded<ScheduledWorkItem>()  // Low
            };
            _backlog[WorkPriority.High] = 0;
            _backlog[WorkPriority.Normal] = 0;
            _backlog[WorkPriority.Low] = 0;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                for (int i = 0; i < _config.MinWorkers; i++)
                {
                    AddWorker();
                }

                _managementLoop = Task.Run(() => ManagementLoopAsync(_cts.Token));
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            if (_managementLoop != null)
            {
                await _managementLoop.ConfigureAwait(false);
            }

            var tasks = _workers.Values.Select(w => w.StopAsync());
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public async Task EnqueueAsync(ScheduledWorkItem item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var idx = (int)item.Priority;
            await _channels[idx].Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            _backlog.AddOrUpdate(item.Priority, 1, (_, v) => v + 1);
            _metrics?.TaskQueued(item);
        }

        private async Task<ScheduledWorkItem?> FetchNextAsync(CancellationToken cancellationToken)
        {
            // Weighted fair selection with aging - try non-blocking reads in computed order
            var now = DateTime.UtcNow;
            var weights = new Dictionary<WorkPriority, double>
            {
                { WorkPriority.High, _config.HighPriorityWeight },
                { WorkPriority.Normal, _config.NormalPriorityWeight },
                { WorkPriority.Low, _config.LowPriorityWeight }
            };

            // compute effective weights adding aging
            foreach (var p in weights.Keys.ToList())
            {
                int cnt = _backlog.TryGetValue(p, out var c) ? c : 0;
                // aging: if there are items, approximate oldest age by now - enqueue for first item is unknown; we use simple factor
                double ageSeconds = 0.0;
                if (cnt > 0)
                {
                    ageSeconds = 1.0; // conservative baseline to nudge aged queues; in production we'd track oldest timestamp per queue
                }

                weights[p] = weights[p] + ageSeconds * _config.AgingFactor;
            }

            // build a selection sequence
            var pickOrder = weights.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToArray();

            // attempt immediate reads in order
            foreach (var p in pickOrder)
            {
                var reader = _channels[(int)p].Reader;
                if (reader.TryRead(out var item))
                {
                    DecrementBacklog(p);
                    _metrics?.TaskDequeued(item);
                    return item;
                }
            }

            // if none available, wait for any to have data
            var waiters = _channels.Select(ch => ch.Reader.WaitToReadAsync(cancellationToken).AsTask()).ToArray();
            var completed = await Task.WhenAny(waiters).ConfigureAwait(false);
            // after any has data, retry a quick non-blocking read in order
            foreach (var p in pickOrder)
            {
                var reader = _channels[(int)p].Reader;
                if (reader.TryRead(out var item))
                {
                    DecrementBacklog(p);
                    _metrics?.TaskDequeued(item);
                    return item;
                }
            }

            return null;
        }

        private void DecrementBacklog(WorkPriority p)
        {
            _backlog.AddOrUpdate(p, 0, (_, v) => Math.Max(0, v - 1));
        }

        private void AddWorker()
        {
            var worker = new Worker(FetchNextAsync, _metrics);
            if (_workers.TryAdd(worker.Id, worker))
            {
                worker.Start();
                _metrics?.ScaleEvent(_workers.Count - 1, _workers.Count);
            }
        }

        private void RemoveIdleWorkers()
        {
            var now = DateTime.UtcNow;
            var idle = _workers.Values.Where(w => (now - w.LastActiveUtc) > _config.IdleTimeout).ToList();
            foreach (var w in idle)
            {
                if (_workers.Count <= _config.MinWorkers) break;
                if (_workers.TryRemove(w.Id, out var removed))
                {
                    _ = removed.StopAsync();
                    _metrics?.ScaleEvent(_workers.Count + 1, _workers.Count);
                }
            }
        }

        private async Task ManagementLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.ManagementInterval, cancellationToken).ConfigureAwait(false);

                    var high = GetQueueCount(WorkPriority.High);
                    var normal = GetQueueCount(WorkPriority.Normal);
                    var low = GetQueueCount(WorkPriority.Low);
                    _metrics?.BacklogSnapshot(high, normal, low);

                    int totalBacklog = high + normal + low;
                    int currentWorkers = _workers.Count;
                    int desired = Math.Max(_config.MinWorkers, Math.Min(_config.MaxWorkers, (int)Math.Ceiling((double)totalBacklog / Math.Max(1, _config.BacklogPerWorkerScaleOut))));

                    if (desired > currentWorkers)
                    {
                        // scale out
                        int toAdd = Math.Min(desired - currentWorkers, _config.MaxWorkers - currentWorkers);
                        for (int i = 0; i < toAdd; i++) AddWorker();
                    }
                    else if (desired < currentWorkers)
                    {
                        // scale in (remove idle workers)
                        RemoveIdleWorkers();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // swallow management exceptions - could log
                }
            }
        }

        private int GetQueueCount(WorkPriority p)
        {
            return _backlog.TryGetValue(p, out var v) ? v : 0;
        }

        public (int High, int Normal, int Low) GetBacklogSnapshot()
        {
            return (GetQueueCount(WorkPriority.High), GetQueueCount(WorkPriority.Normal), GetQueueCount(WorkPriority.Low));
        }
    }
}
