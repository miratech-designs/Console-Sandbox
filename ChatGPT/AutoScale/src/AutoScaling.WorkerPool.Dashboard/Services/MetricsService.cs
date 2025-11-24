using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoScaling.WorkerPool.Dashboard.Services
{
    public record BacklogSample(DateTime Utc, int High, int Normal, int Low);
    public record ScaleEventSample(DateTime Utc, int OldCount, int NewCount);
    public record TaskDurationSample(DateTime Utc, double Milliseconds);

    public class MetricsService : IDisposable
    {
        private readonly ConcurrentQueue<BacklogSample> _backlog = new();
        private readonly ConcurrentQueue<ScaleEventSample> _scales = new();
        private readonly ConcurrentQueue<TaskDurationSample> _durations = new();
        private readonly Timer _trimTimer;

        // keep last N seconds of data (default 5 minutes)
        private readonly TimeSpan _retention = TimeSpan.FromMinutes(5);

        public MetricsService()
        {
            _trimTimer = new Timer(_ => Trim(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void AddBacklog(int high, int normal, int low)
        {
            _backlog.Enqueue(new BacklogSample(DateTime.UtcNow, high, normal, low));
        }

        public void AddScaleEvent(int oldCount, int newCount)
        {
            _scales.Enqueue(new ScaleEventSample(DateTime.UtcNow, oldCount, newCount));
        }

        public void AddTaskDuration(TimeSpan duration)
        {
            _durations.Enqueue(new TaskDurationSample(DateTime.UtcNow, duration.TotalMilliseconds));
        }

        public IReadOnlyList<BacklogSample> GetBacklogSeries()
        {
            return _backlog.ToArray();
        }

        public IReadOnlyList<ScaleEventSample> GetScaleEvents()
        {
            return _scales.ToArray();
        }

        public IReadOnlyList<TaskDurationSample> GetTaskDurations()
        {
            return _durations.ToArray();
        }

        private void Trim()
        {
            var cutoff = DateTime.UtcNow - _retention;
            while (_backlog.TryPeek(out var b) && b.Utc < cutoff) _backlog.TryDequeue(out _);
            while (_scales.TryPeek(out var s) && s.Utc < cutoff) _scales.TryDequeue(out _);
            while (_durations.TryPeek(out var d) && d.Utc < cutoff) _durations.TryDequeue(out _);
        }

        public void Dispose()
        {
            _trimTimer?.Dispose();
        }
    }
}
