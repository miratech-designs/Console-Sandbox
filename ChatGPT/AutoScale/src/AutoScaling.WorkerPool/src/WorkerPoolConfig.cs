using System;

namespace AutoScaling.WorkerPool
{
    public class WorkerPoolConfig
    {
        public int MinWorkers { get; set; } = 1;
        public int MaxWorkers { get; set; } = Environment.ProcessorCount;
        public int BacklogPerWorkerScaleOut { get; set; } = 4;
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(20);
        public int HighPriorityWeight { get; set; } = 8;
        public int NormalPriorityWeight { get; set; } = 3;
        public int LowPriorityWeight { get; set; } = 1;
        public double AgingFactor { get; set; } = 0.1; // weight/sec
        public TimeSpan ManagementInterval { get; set; } = TimeSpan.FromSeconds(1);
    }
}
