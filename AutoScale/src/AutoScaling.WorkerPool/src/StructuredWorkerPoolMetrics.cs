using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoScaling.WorkerPool
{
    public sealed class StructuredWorkerPoolMetrics : IWorkerPoolMetrics
    {
        private readonly ILogger _logger;

        public StructuredWorkerPoolMetrics(ILogger? logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public void WorkerCreated(string workerId)
        {
            _logger.LogInformation("WorkerCreated {WorkerId}", workerId);
        }

        public void WorkerDestroyed(string workerId)
        {
            _logger.LogInformation("WorkerDestroyed {WorkerId}", workerId);
        }

        public void TaskQueued(ScheduledWorkItem item)
        {
            _logger.LogInformation("TaskQueued {TaskId} Priority={Priority} Name={Name}", item.Id, item.Priority, item.Name);
        }

        public void TaskDequeued(ScheduledWorkItem item)
        {
            _logger.LogInformation("TaskDequeued {TaskId} Priority={Priority} Name={Name}", item.Id, item.Priority, item.Name);
        }

        public void TaskCompleted(ScheduledWorkItem item, TimeSpan duration, bool succeeded)
        {
            _logger.LogInformation("TaskCompleted {TaskId} Priority={Priority} DurationMs={DurationMs} Success={Success}", item.Id, item.Priority, duration.TotalMilliseconds, succeeded);
        }

        public void ScaleEvent(int oldCount, int newCount)
        {
            _logger.LogInformation("ScaleEvent {OldCount} -> {NewCount}", oldCount, newCount);
        }

        public void BacklogSnapshot(int high, int normal, int low)
        {
            _logger.LogInformation("Backlog Snapshot High={High} Normal={Normal} Low={Low}", high, normal, low);
        }
    }
}
