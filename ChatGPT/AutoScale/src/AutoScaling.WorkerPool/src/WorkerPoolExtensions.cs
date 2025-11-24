using Microsoft.Extensions.DependencyInjection;
using System;

namespace AutoScaling.WorkerPool
{
    public static class WorkerPoolExtensions
    {
        public static IServiceCollection AddAutoScalingWorkerPool(this IServiceCollection services, Action<WorkerPoolConfig>? configure = null)
        {
            var config = new WorkerPoolConfig();
            configure?.Invoke(config);
            services.AddSingleton(config);
            // Register a default metrics sink (console). Consumers may override by registering
            // a different `IWorkerPoolMetrics` before resolving the pool.
            services.AddSingleton<IWorkerPoolMetrics, ConsoleWorkerPoolMetrics>();
            services.AddSingleton<IWorkerPool, PriorityAutoScalingWorkerPool>(sp => new PriorityAutoScalingWorkerPool(config, sp.GetRequiredService<IWorkerPoolMetrics>()));
            return services;
        }
    }
}
