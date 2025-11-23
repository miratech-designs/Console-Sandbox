public interface IWorkerPoolMetrics
{
    void ReportQueueDepth(WorkPriority priority, int depth);
    void ReportOldestAge(WorkPriority priority, double ageSeconds);
    void ReportTaskCompleted(WorkPriority priority);
    void ReportWorkerCount(int workers);
    void ReportScaleEvent(string direction, int newCount);
}

public class ConsoleWorkerPoolMetrics : IWorkerPoolMetrics
{
    public void ReportQueueDepth(WorkPriority priority, int depth)
    {
        Console.WriteLine($"QueueDepth[{priority}] = {depth}");
    }

    public void ReportOldestAge(WorkPriority priority, double ageSeconds)
    {
        Console.WriteLine($"OldestAge[{priority}] = {ageSeconds:F1}s");
    }

    public void ReportTaskCompleted(WorkPriority priority)
    {
        Console.WriteLine($"TaskCompleted[{priority}]");
    }

    public void ReportWorkerCount(int count)
    {
        Console.WriteLine($"Workers = {count}");
    }

    public void ReportScaleEvent(string direction, int newCount)
    {
        Console.WriteLine($"ScaleEvent: {direction} → {newCount} workers");
    }
}
public class PriorityWorkerPoolConfig : WorkerPoolConfig
{
    public Dictionary<WorkPriority, int> PriorityWeights { get; init; } = new()
    {
        { WorkPriority.High, 5 },
        { WorkPriority.Normal, 2 },
        { WorkPriority.Low, 1 }
    };

    public double AgingFactor { get; init; } = 0.2;
    public IWorkerPoolMetrics Metrics { get; init; } = new ConsoleWorkerPoolMetrics();
}

private async Task AutoscalerLoop()
{
    while (!_globalCts.Token.IsCancellationRequested)
    {
        await Task.Delay(_config.MetricsInterval);

        // Report queue depth + oldest age
        foreach (var (priority, channel) in _channels)
        {
            int depth = channel.Reader.Count;
            _config.Metrics.ReportQueueDepth(priority, depth);

            DateTime oldest = _oldestTimestamp[priority];
            if (oldest != DateTime.MaxValue)
            {
                double age = (DateTime.UtcNow - oldest).TotalSeconds;
                _config.Metrics.ReportOldestAge(priority, age);
            }
        }

        // Report worker count
        _config.Metrics.ReportWorkerCount(_workers.Count);

        // Scaling decisions...
        // (existing autoscaling code)
    }
}

if (reader.TryRead(out var item))
{
    UpdateOldestTimestamp(selectedPriority);

    try
    {
        await item.Work();
        _config.Metrics.ReportTaskCompleted(selectedPriority);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Worker error: {ex}");
    }
}

// add worker
_config.Metrics.ReportScaleEvent("ScaleOut", _workers.Count);

// remove worker
_config.Metrics.ReportScaleEvent("ScaleIn", _workers.Count);

public class PrometheusWorkerPoolMetrics : IWorkerPoolMetrics
{
    private readonly Gauge _queueDepth =
        Metrics.CreateGauge("workerpool_queue_depth", "Queue depth", "priority");

    private readonly Gauge _oldestAge =
        Metrics.CreateGauge("workerpool_oldest_age_seconds", "Oldest item age", "priority");

    private readonly Counter _completed =
        Metrics.CreateCounter("workerpool_tasks_completed_total", "Tasks completed", "priority");

    private readonly Gauge _workerCount =
        Metrics.CreateGauge("workerpool_workers", "Active worker count");

    private readonly Counter _scaleEvents =
        Metrics.CreateCounter("workerpool_scale_events_total", "Scale events", "direction");

    public void ReportQueueDepth(WorkPriority priority, int depth)
        => _queueDepth.WithLabels(priority.ToString()).Set(depth);

    public void ReportOldestAge(WorkPriority priority, double ageSeconds)
        => _oldestAge.WithLabels(priority.ToString()).Set(ageSeconds);

    public void ReportTaskCompleted(WorkPriority priority)
        => _completed.WithLabels(priority.ToString()).Inc();

    public void ReportWorkerCount(int workers)
        => _workerCount.Set(workers);

    public void ReportScaleEvent(string direction, int newCount)
        => _scaleEvents.WithLabels(direction).Inc();
}

using Prometheus;
var server = new KestrelMetricServer(port: 9090);
server.Start();

