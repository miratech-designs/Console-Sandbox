using System;
using Prometheus;

namespace AutoScaling.WorkerPool
{
    public sealed class PrometheusWorkerPoolMetrics : IWorkerPoolMetrics, IDisposable
    {
        private readonly Counter _tasksQueued;
        private readonly Counter _tasksDequeued;
        private readonly Counter _tasksCompleted;
        private readonly Histogram _taskDuration;
        private readonly Gauge _workers;
        private readonly Gauge _backlog;

        public PrometheusWorkerPoolMetrics()
        {
            _tasksQueued = Metrics.CreateCounter("autoscaling_workerpool_tasks_queued_total", "Total number of tasks queued", new CounterConfiguration
            {
                LabelNames = new[] { "priority" }
            });

            _tasksDequeued = Metrics.CreateCounter("autoscaling_workerpool_tasks_dequeued_total", "Total number of tasks dequeued", new CounterConfiguration
            {
                LabelNames = new[] { "priority" }
            });

            _tasksCompleted = Metrics.CreateCounter("autoscaling_workerpool_tasks_completed_total", "Total number of tasks completed", new CounterConfiguration
            {
                LabelNames = new[] { "priority", "success" }
            });

            _taskDuration = Metrics.CreateHistogram("autoscaling_workerpool_task_duration_seconds", "Task execution duration in seconds", new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 12),
                LabelNames = new[] { "priority" }
            });

            _workers = Metrics.CreateGauge("autoscaling_workerpool_workers", "Current number of active workers");

            _backlog = Metrics.CreateGauge("autoscaling_workerpool_backlog", "Current backlog per priority", new GaugeConfiguration
            {
                LabelNames = new[] { "priority" }
            });
        }

        private static string LabelForPriority(ScheduledWorkItem item)
            => item?.Priority.ToString() ?? "Unknown";

        private static string LabelForPriorityName(string name) => name;

        public void WorkerCreated(string workerId)
        {
            _workers.Inc();
        }

        public void WorkerDestroyed(string workerId)
        {
            _workers.Dec();
        }

        public void TaskQueued(ScheduledWorkItem item)
        {
            _tasksQueued.WithLabels(LabelForPriority(item)).Inc();
            // update backlog gauge for this priority incrementally is left to BacklogSnapshot
        }

        public void TaskDequeued(ScheduledWorkItem item)
        {
            _tasksDequeued.WithLabels(LabelForPriority(item)).Inc();
        }

        public void TaskCompleted(ScheduledWorkItem item, TimeSpan duration, bool succeeded)
        {
            _tasksCompleted.WithLabels(LabelForPriority(item), succeeded ? "true" : "false").Inc();
            _taskDuration.WithLabels(LabelForPriority(item)).Observe(duration.TotalSeconds);
        }

        public void ScaleEvent(int oldCount, int newCount)
        {
            // reflect desired worker gauge value
            _workers.Set(newCount);
        }

        public void BacklogSnapshot(int high, int normal, int low)
        {
            _backlog.WithLabels("High").Set(high);
            _backlog.WithLabels("Normal").Set(normal);
            _backlog.WithLabels("Low").Set(low);
        }

        public void Dispose()
        {
            // prometheus-net does not require explicit disposal of metrics, but implement IDisposable for completeness.
        }
    }
}
