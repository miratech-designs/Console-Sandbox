using System;
using System.Threading.Tasks;
using System.Threading;
using AutoScaling.WorkerPool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ErrorHandler;

Console.WriteLine("Starting Console Example of AutoScaling.WorkerPool...");

var services = new ServiceCollection();
services.AddLogging(cfg => cfg.AddSimpleConsole(options => { options.SingleLine = true; }));

// Register the global error handler
services.AddGlobalErrorHandler(opts =>
{
    opts.ExitCode = 1;
    opts.Rethrow = false;
    opts.OnExceptionAsync = async (ex, ct) =>
    {
        // Example: additional handling (telemetry) can go here
        await Task.CompletedTask;
    };
});

using var provider = services.BuildServiceProvider();

await GlobalErrorHandling.RunWithGlobalHandlerAsync(provider, async () =>
{
    var config = new WorkerPoolConfig
    {
        MinWorkers = 1,
        MaxWorkers = 8,
        BacklogPerWorkerScaleOut = 4,
        IdleTimeout = TimeSpan.FromSeconds(5),
        AgingFactor = 0.5
    };

    var metrics = new ConsoleWorkerPoolMetrics();
    var pool = new PriorityAutoScalingWorkerPool(config, metrics);
    var cts = new CancellationTokenSource();

    await pool.StartAsync(cts.Token);

    var rnd = new Random();
    for (int i = 0; i < 50; i++)
    {
        var priority = (WorkPriority)(rnd.Next(0, 3));
        var idx = i;
        var item = new ScheduledWorkItem(priority, async token =>
        {
            var delay = rnd.Next(100, 1000);
            Console.WriteLine($"[Task {idx}] Starting (P={priority}) sleeping {delay}ms");
            await Task.Delay(delay, token);
            Console.WriteLine($"[Task {idx}] Completed");
        }, name: $"Task-{i}");

        await pool.EnqueueAsync(item);
        await Task.Delay(rnd.Next(20, 80));
    }

    Console.WriteLine("All tasks enqueued. Press Ctrl+C to stop or wait 20s for graceful shutdown.");

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(20), cts.Token);
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Shutting down pool...");
    await pool.StopAsync();
    Console.WriteLine("Shutdown complete.");
});
