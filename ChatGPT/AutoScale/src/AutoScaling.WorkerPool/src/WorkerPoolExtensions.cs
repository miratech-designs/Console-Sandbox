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
            services.AddSingleton<IWorkerPool, PriorityAutoScalingWorkerPool>(sp => new PriorityAutoScalingWorkerPool(config, sp.GetService<IWorkerPoolMetrics>()));
            services.AddSingleton<IWorkerPoolMetrics, ConsoleWorkerPoolMetrics>();
            return services;
        }
    }
}
