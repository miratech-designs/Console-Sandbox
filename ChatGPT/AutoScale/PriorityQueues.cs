/*
Below is a clean, production-ready design for a multi-priority auto-scaling worker pool.
You get:

✔ Multiple priority queues (High / Normal / Low — or however many you want)
✔ A single auto-scaling worker pool processing across all priorities
✔ Workers always pull highest-priority work first
✔ Each priority gets its own channel
✔ Backlog-aware scaling considers weighted priority
✔ Nothing breaks your existing design

This pattern is scalable, simple, and still works inside a .NET console app.
*/
public enum WorkPriority
{
    High = 0,
    Normal = 1,
    Low = 2
}
using System.Threading.Channels;

public class PriorityWorkerPoolConfig : WorkerPoolConfig
{
    public Dictionary<WorkPriority, int> PriorityWeights { get; init; } = new()
    {
        { WorkPriority.High, 5 },
        { WorkPriority.Normal, 2 },
        { WorkPriority.Low, 1 }
    };
}

public class PriorityAutoScalingWorkerPool
{
    private readonly PriorityWorkerPoolConfig _config;

    private readonly Dictionary<WorkPriority, Channel<Func<Task>>> _channels;
    private readonly List<(Task worker, CancellationTokenSource cts)> _workers = new();

    private readonly CancellationTokenSource _globalCts = new();
    private DateTime _lastScaleAction = DateTime.MinValue;

    public PriorityAutoScalingWorkerPool(PriorityWorkerPoolConfig config)
    {
        _config = config;

        _channels = Enum.GetValues<WorkPriority>()
            .ToDictionary(
                p => p,
                p => Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                })
            );
    }

    // ---------- Public API ----------
    public ChannelWriter<Func<Task>> GetWriter(WorkPriority priority)
        => _channels[priority].Writer;

    public int CurrentWorkers => _workers.Count;

    public void Start()
    {
        for (int i = 0; i < _config.MinWorkers; i++)
            AddWorker();

        _ = Task.Run(AutoscalerLoop);
    }

    public async Task StopAsync()
    {
        _globalCts.Cancel();

        foreach (var ch in _channels.Values)
            ch.Writer.TryComplete();

        foreach (var w in _workers)
            w.cts.Cancel();

        await Task.WhenAll(_workers.Select(w => w.worker));
    }

    // ---------- Worker Loop ----------
    private async Task WorkerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool foundWork = false;

            // Priority-based scanning: High → Normal → Low
            foreach (var p in Enum.GetValues<WorkPriority>().OrderBy(p => p))
            {
                var reader = _channels[p].Reader;

                while (reader.TryRead(out var workItem))
                {
                    foundWork = true;
                    try { await workItem(); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Worker error: {ex}");
                    }
                }

                if (foundWork)
                    break; // don't check lower priorities if we found work
            }

            if (!foundWork)
            {
                // Nothing available at ANY priority → wait until something arrives
                await Task.WhenAny(
                    _channels.Values.Select(c => c.Reader.WaitToReadAsync(token).AsTask())
                );
            }
        }
    }

    // ---------- Worker Management ----------
    private void AddWorker()
    {
        if (_workers.Count >= _config.MaxWorkers)
            return;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        var task = Task.Run(() => WorkerLoop(cts.Token));

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

    // ---------- Auto-scaler loop ----------
    private async Task AutoscalerLoop()
    {
        while (!_globalCts.Token.IsCancellationRequested)
        {
            await Task.Delay(_config.MetricsInterval);

            int weightedBacklog = 0;

            foreach (var (priority, channel) in _channels)
            {
                int backlog = channel.Reader.Count;
                weightedBacklog += backlog * _config.PriorityWeights[priority];
            }

            int w = _workers.Count;

            bool cooldown = (DateTime.UtcNow - _lastScaleAction) < _config.ScaleCooldown;
            if (cooldown) continue;

            // scale out
            if (weightedBacklog > w * _config.BacklogPerWorkerScaleOut)
            {
                AddWorker();
                continue;
            }

            // scale in
            if (weightedBacklog == 0)
            {
                RemoveWorker();
                continue;
            }
        }
    }
}

static async Task Main()
{
    var pool = new PriorityAutoScalingWorkerPool(new PriorityWorkerPoolConfig
    {
        MinWorkers = 2,
        MaxWorkers = 30,
        BacklogPerWorkerScaleOut = 10,
        MetricsInterval = TimeSpan.FromSeconds(1),
        ScaleCooldown = TimeSpan.FromSeconds(5),

        PriorityWeights = new()
        {
            { WorkPriority.High,    10 },
            { WorkPriority.Normal,   3 },
            { WorkPriority.Low,      1 }
        }
    });

    pool.Start();

    var high = pool.GetWriter(WorkPriority.High);
    var normal = pool.GetWriter(WorkPriority.Normal);
    var low = pool.GetWriter(WorkPriority.Low);

    // Simulated workload
    _ = Task.Run(async () =>
    {
        Random rand = new();

        while (true)
        {
            // random priority dispatch
            var pr = (WorkPriority)rand.Next(0, 3);

            var writer = pool.GetWriter(pr);
            await writer.WriteAsync(async () =>
            {
                await Task.Delay(200);
                Console.WriteLine($"Processed {pr} priority work");
            });

            await Task.Delay(rand.Next(5, 40));
        }
    });

    Console.WriteLine("Press Enter to quit");
    Console.ReadLine();

    await pool.StopAsync();
}
