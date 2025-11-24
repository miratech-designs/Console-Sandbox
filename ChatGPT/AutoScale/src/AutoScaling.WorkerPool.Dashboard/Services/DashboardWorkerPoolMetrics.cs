using System;
using AutoScaling.WorkerPool;

namespace AutoScaling.WorkerPool.Dashboard.Services
{
    // Composite metrics sink: forwards to inner (e.g., Prometheus) and notifies the MetricsService
    public class DashboardWorkerPoolMetrics : IWorkerPoolMetrics
    {
        private readonly IWorkerPoolMetrics? _inner;
        private readonly MetricsService _svc;

        public DashboardWorkerPoolMetrics(IWorkerPoolMetrics? inner, MetricsService svc)
        {
            _inner = inner;
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        public void BacklogSnapshot(int high, int normal, int low)
        {
            _inner?.BacklogSnapshot(high, normal, low);
            _svc.AddBacklog(high, normal, low);
        }

        public void ScaleEvent(int oldCount, int newCount)
        {
            _inner?.ScaleEvent(oldCount, newCount);
            _svc.AddScaleEvent(oldCount, newCount);
        }

        public void TaskCompleted(ScheduledWorkItem item, TimeSpan duration, bool succeeded)
        {
            _inner?.TaskCompleted(item, duration, succeeded);
            _svc.AddTaskDuration(duration);
        }

        public void TaskDequeued(ScheduledWorkItem item)
        {
            _inner?.TaskDequeued(item);
        }

        public void TaskQueued(ScheduledWorkItem item)
        {
            _inner?.TaskQueued(item);
        }

        public void WorkerCreated(string workerId)
        {
            _inner?.WorkerCreated(workerId);
        }

        public void WorkerDestroyed(string workerId)
        {
            _inner?.WorkerDestroyed(workerId);
        }
    }
}
