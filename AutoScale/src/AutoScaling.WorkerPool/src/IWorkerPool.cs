using System.Threading;
using System.Threading.Tasks;

namespace AutoScaling.WorkerPool
{
    public interface IWorkerPool
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        Task EnqueueAsync(ScheduledWorkItem item, CancellationToken cancellationToken = default);
    }
}
