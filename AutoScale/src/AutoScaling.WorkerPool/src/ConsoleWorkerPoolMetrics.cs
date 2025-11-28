using System;
using System.Threading;

namespace AutoScaling.WorkerPool
{
    public class ConsoleWorkerPoolMetrics : IWorkerPoolMetrics
    {
        private long _queued;
        private long _dequeued;

        public void WorkerCreated(string workerId)
        {
            Console.WriteLine($"[Metrics] Worker created: {workerId}");
        }

        public void WorkerDestroyed(string workerId)
        {
            Console.WriteLine($"[Metrics] Worker destroyed: {workerId}");
        }

        public void TaskQueued(ScheduledWorkItem item)
        {
            Interlocked.Increment(ref _queued);
            Console.WriteLine($"[Metrics] Task queued: {item.Id} ({item.Priority})");
        }

        public void TaskDequeued(ScheduledWorkItem item)
        {
            Interlocked.Increment(ref _dequeued);
            Console.WriteLine($"[Metrics] Task dequeued: {item.Id} ({item.Priority})");
        }

        public void TaskCompleted(ScheduledWorkItem item, TimeSpan duration, bool succeeded)
        {
            Console.WriteLine($"[Metrics] Task completed: {item.Id} ({item.Priority}) in {duration.TotalMilliseconds}ms - Success={succeeded}");
        }

        public void ScaleEvent(int oldCount, int newCount)
        {
            Console.WriteLine($"[Metrics] Scale event: {oldCount} -> {newCount}");
        }

        public void BacklogSnapshot(int high, int normal, int low)
        {
            Console.WriteLine($"[Metrics] Backlog H:{high} N:{normal} L:{low}");
        }
    }
}
