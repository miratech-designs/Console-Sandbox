using AutoScaling.WorkerPool;
using Prometheus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder();

builder.Services.AddAutoScalingWorkerPool(cfg =>
{
    cfg.MinWorkers = 1;
    cfg.MaxWorkers = 6;
    cfg.BacklogPerWorkerScaleOut = 4;
    cfg.IdleTimeout = System.TimeSpan.FromSeconds(10);
});

// Register Prometheus metrics sink for scraping at /metrics
builder.Services.AddSingleton<IWorkerPoolMetrics, PrometheusWorkerPoolMetrics>();

var app = builder.Build();

var pool = app.Services.GetRequiredService<IWorkerPool>();
_ = pool.StartAsync();

app.MapPost("/enqueue/{priority}", async (string priority) =>
{
    if (!Enum.TryParse<WorkPriority>(priority, true, out var p)) return Results.BadRequest("invalid priority");
    var item = new ScheduledWorkItem(p, async ct =>
    {
        await Task.Delay(500, ct);
    });
    await pool.EnqueueAsync(item);
    return Results.Accepted($"enqueued {item.Id}");
});

app.MapGet("/status", () =>
{
    if (pool is PriorityAutoScalingWorkerPool p)
    {
        var snap = p.GetBacklogSnapshot();
        return Results.Ok(new { workers = "managed by host", backlog = snap });
    }

    return Results.Ok(new { workers = "unknown", backlog = new { High = 0, Normal = 0, Low = 0 } });
});

app.Lifetime.ApplicationStopping.Register(() => pool.StopAsync().GetAwaiter().GetResult());

// Expose Prometheus metrics at /metrics
app.UseMetricServer();
app.UseHttpMetrics();

app.Run();
