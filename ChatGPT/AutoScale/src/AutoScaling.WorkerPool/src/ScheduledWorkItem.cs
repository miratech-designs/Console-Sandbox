using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoScaling.WorkerPool
{
    public sealed class ScheduledWorkItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string? Name { get; set; }
        public WorkPriority Priority { get; }
        public DateTime EnqueuedAtUtc { get; } = DateTime.UtcNow;
        public Func<CancellationToken, Task> Work { get; }

        public ScheduledWorkItem(WorkPriority priority, Func<CancellationToken, Task> work, string? name = null)
        {
            Priority = priority;
            Work = work ?? throw new ArgumentNullException(nameof(work));
            Name = name;
        }
    }
}
