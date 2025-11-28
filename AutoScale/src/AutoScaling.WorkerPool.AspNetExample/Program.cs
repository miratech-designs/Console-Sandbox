using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AutoScaling.WorkerPool;
using Prometheus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Extensions.Hosting;
using ErrorHandler;

var activitySource = new ActivitySource("AutoScaling.WorkerPool.AspNetExample");

// Configure Serilog early so framework logs get captured
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder();
builder.Host.UseSerilog();

// Note: OpenTelemetry instrumentation and exporters can be added here.
// See ErrorHandler/README.md for an example of wiring OpenTelemetry and Serilog.

builder.Services.AddAutoScalingWorkerPool(cfg =>
{
    cfg.MinWorkers = 1;
    cfg.MaxWorkers = 6;
    cfg.BacklogPerWorkerScaleOut = 4;
    cfg.IdleTimeout = TimeSpan.FromSeconds(10);
});

// Register Prometheus metrics sink for scraping at /metrics
builder.Services.AddSingleton<IWorkerPoolMetrics, PrometheusWorkerPoolMetrics>();

// Register the global error handler; forward exceptions to Serilog + OpenTelemetry activity
builder.Services.AddGlobalErrorHandler(opts =>
{
    opts.ExitCode = 1;
    opts.OnExceptionAsync = async (ex, ct) =>
    {
        Log.Fatal(ex, "Unhandled exception captured by GlobalErrorHandler");

        using var activity = activitySource.StartActivity("UnhandledException", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.message", ex.Message);
            activity.SetStatus(ActivityStatusCode.Error);
            activity.RecordException(ex);
        }

        await Task.CompletedTask;
    };
});

var app = builder.Build();

// Wire process-level handlers so AppDomain/TaskScheduler exceptions are also routed
using var registration = app.RegisterProcessLevelHandlers();

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

app.Lifetime.ApplicationStopping.Register(() =>
{
    try { registration.Dispose(); } catch { }
    pool.StopAsync().GetAwaiter().GetResult();
});

// Expose Prometheus metrics at /metrics
app.UseMetricServer();
app.UseHttpMetrics();

app.Run();
