using AutoScaling.WorkerPool;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;

var builder = WebApplication.CreateBuilder();

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Dashboard metrics service (collects runtime snapshots/events)
builder.Services.AddSingleton<AutoScaling.WorkerPool.Dashboard.Services.MetricsService>();

// Register Prometheus sink then a composite dashboard sink that forwards to Prometheus and the MetricsService
builder.Services.AddSingleton<PrometheusWorkerPoolMetrics>();
builder.Services.AddSingleton<IWorkerPoolMetrics>(sp =>
{
    var prom = sp.GetRequiredService<PrometheusWorkerPoolMetrics>();
    var svc = sp.GetRequiredService<AutoScaling.WorkerPool.Dashboard.Services.MetricsService>();
    return new AutoScaling.WorkerPool.Dashboard.Services.DashboardWorkerPoolMetrics(prom, svc);
});

// Configure worker pool and metrics (Dashboard + Prometheus)
builder.Services.AddAutoScalingWorkerPool(cfg =>
{
    cfg.MinWorkers = 1;
    cfg.MaxWorkers = 8;
    cfg.BacklogPerWorkerScaleOut = 4;
    cfg.IdleTimeout = System.TimeSpan.FromSeconds(15);
    cfg.ManagementInterval = System.TimeSpan.FromSeconds(2);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

// Expose Prometheus metrics
app.UseMetricServer();
app.UseHttpMetrics();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Start the worker pool when app starts
var pool = app.Services.GetRequiredService<IWorkerPool>();
_ = pool.StartAsync();

app.Lifetime.ApplicationStopping.Register(() => pool.StopAsync().GetAwaiter().GetResult());

app.Run();
