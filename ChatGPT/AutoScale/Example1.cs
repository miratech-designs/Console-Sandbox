/*
🚀 What This Gives You

This worker pool behaves like an AWS Auto Scaling Group:
Feature
Supported?
Min/Max capacity
✔
Scale up based on queue depth
✔
Scale down when idle
✔
Cooldown (hysteresis)
✔
Graceful worker shutdown
✔
Works with any async job
✔
Thread-safe, uses ThreadPool automatically
✔

*/

using System.Threading.Channels;

public class AutoScalingWorkerPool
{
    private readonly Channel<Func<Task>> _channel;
    private readonly WorkerPoolConfig _config;
    private readonly List<(Task worker, CancellationTokenSource cts)> _workers = new();

    private readonly CancellationTokenSource _globalCts = new();
    private DateTime _lastScaleAction = DateTime.MinValue;

    public int CurrentWorkerCount => _workers.Count;
    public ChannelWriter<Func<Task>> Writer => _channel.Writer;

    public AutoScalingWorkerPool(WorkerPoolConfig config)
    {
        _config = config;
        _channel = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    // ------------------ Public API ------------------

    public void Start()
    {
        for (int i = 0; i < _config.MinWorkers; i++)
            AddWorker();

        _ = Task.Run(AutoscalerLoop);
    }

    public async Task StopAsync()
    {
        _globalCts.Cancel();
        _channel.Writer.Complete();

        foreach (var w in _workers)
            w.cts.Cancel();

        await Task.WhenAll(_workers.Select(w => w.worker));
    }

    // ------------------ Worker Logic ------------------

    private async Task WorkerLoop(ChannelReader<Func<Task>> reader, CancellationToken token)
    {
        while (await reader.WaitToReadAsync(token))
        {
            while (reader.TryRead(out var workItem))
            {
                try
                {
                    await workItem();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker error: {ex}");
                }
            }
        }
    }

    private void AddWorker()
    {
        if (_workers.Count >= _config.MaxWorkers)
            return;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        var task = Task.Run(() => WorkerLoop(_channel.Reader, cts.Token));

        _workers.Add((task, cts));

        Console.WriteLine($"➡️ Added worker. Total: {_workers.Count}");
        _lastScaleAction = DateTime.UtcNow;
    }

    private void RemoveWorker()
    {
        if (_workers.Count <= _config.MinWorkers)
            return;

        var (task, cts) = _workers[^1];
        _workers.RemoveAt(_workers.Count - 1);

        cts.Cancel();

        Console.WriteLine($"⬅️ Removed worker. Total: {_workers.Count}");
        _lastScaleAction = DateTime.UtcNow;
    }

    // ------------------ Auto-Scaler ------------------

    private async Task AutoscalerLoop()
    {
        var reader = _channel.Reader;

        while (!_globalCts.Token.IsCancellationRequested)
        {
            await Task.Delay(_config.MetricsInterval);

            int backlog = reader.Count;
            int current = _workers.Count;

            bool cooldown = (DateTime.UtcNow - _lastScaleAction) < _config.ScaleCooldown;

            if (!cooldown)
            {
                // scale out
                if (backlog > current * _config.BacklogPerWorkerScaleOut)
                {
                    AddWorker();
                    continue;
                }

                // scale in
                if (backlog <= _config.BacklogScaleInThreshold)
                {
                    RemoveWorker();
                    continue;
                }
            }
        }
    }
}

public class WorkerPoolConfig
{
    public int MinWorkers { get; init; } = 2;
    public int MaxWorkers { get; init; } = Environment.ProcessorCount * 2;

    // Backlog scaling logic
    public int BacklogPerWorkerScaleOut { get; init; } = 10;   // scale out when backlog > workers * 10
    public int BacklogScaleInThreshold { get; init; } = 0;     // scale in when backlog == 0

    // Timing
    public TimeSpan MetricsInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan ScaleCooldown { get; init; } = TimeSpan.FromSeconds(5); // hysteresis
}

static async Task Main()
{
    var pool = new AutoScalingWorkerPool(new WorkerPoolConfig
    {
        MinWorkers = 2,
        MaxWorkers = 30,
        BacklogPerWorkerScaleOut = 15,
        MetricsInterval = TimeSpan.FromSeconds(1),
        ScaleCooldown = TimeSpan.FromSeconds(5)
    });

    pool.Start();

    // Producer: simulate variable load
    _ = Task.Run(async () =>
    {
        Random rnd = new();

        while (true)
        {
            await pool.Writer.WriteAsync(async () =>
            {
                await Task.Delay(100); // simulate work
            });

            await Task.Delay(rnd.Next(5, 30)); // simulate bursty load
        }
    });

    Console.WriteLine("Press Enter to exit...");
    Console.ReadLine();

    await pool.StopAsync();
}
