using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoScaling.WorkerPool
{
    public interface IWorkerPoolMetrics
    {
        void WorkerCreated(string workerId);
        void WorkerDestroyed(string workerId);
        void TaskQueued(ScheduledWorkItem item);
        void TaskDequeued(ScheduledWorkItem item);
        void TaskCompleted(ScheduledWorkItem item, TimeSpan duration, bool succeeded);
        void ScaleEvent(int oldCount, int newCount);
        void BacklogSnapshot(int high, int normal, int low);
    }
}
