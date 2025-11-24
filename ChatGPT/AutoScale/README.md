# AutoScaling.WorkerPool

Overview
- AutoScaling.WorkerPool is a production-focused .NET library providing an autoscaling worker pool with priority scheduling, fair-share selection, aging to prevent starvation, and built-in observability hooks.

Features
- Priority queues: High, Normal, Low (independent channels)
- Fair-share selection with configurable weights
- Aging factor to gradually increase effective priority of older tasks
- Dynamic autoscaling: scale out and scale in based on backlog and idle time
- Observability via `IWorkerPoolMetrics` and `ConsoleWorkerPoolMetrics`
- Examples: Console app and ASP.NET Core minimal API
- NuGet packaging support and multi-targeting (.NET 8 / .NET Standard 2.1)

Installation (NuGet)
Install the package once published:

  dotnet add package AutoScaling.WorkerPool

Quick Start
- Create `WorkerPoolConfig`, optionally register `IWorkerPoolMetrics`, then create `PriorityAutoScalingWorkerPool` and call `StartAsync()`.

Console Example
- See `src/AutoScaling.WorkerPool.ConsoleExample/Program.cs` — enqueues 50 mixed priority tasks and demonstrates autoscaling.

ASP.NET Example
- See `src/AutoScaling.WorkerPool.AspNetExample/Program.cs` — minimal API with `POST /enqueue/{priority}` and `GET /status`.

Architecture Overview
- Queues: separate `Channel<ScheduledWorkItem>` per priority.
- Scheduler: workers fetch tasks using weighted selection plus a small aging bias.
- Autoscaler: management loop inspects backlog, scales out when backlog per worker exceeds `BacklogPerWorkerScaleOut`, and scales in by removing idle workers after `IdleTimeout`.

Priority Queues
- Independent channels guarantee enqueue/dequeue isolation and simple backlog accounting.

Fair-Share Scheduling
- Each priority has a base weight. The scheduler orders priorities by effective weight and attempts to read from the highest effective weight first.

Aging Explained
- An aging factor increases the effective priority of waiting tasks over time to prevent starvation.

Autoscaling Algorithm
- Every `ManagementInterval` the pool examines total backlog and computes a desired worker count:

  desired = clamp(MinWorkers, MaxWorkers, ceil(totalBacklog / BacklogPerWorkerScaleOut))

- Scale out by adding workers. Scale in by removing idle workers beyond `IdleTimeout`.

Observability & Metrics
- Implement `IWorkerPoolMetrics` to receive hooks for worker lifecycle, queue events, task completion, scale events, and backlog snapshots.

ASCII Diagrams

Queue architecture

  [High Channel] -->|
  [Normal Channel] -->|--> Scheduler -> Worker Pool
  [Low Channel]  -->|

Worker scheduling flow

  Enqueue -> Channel (per priority)
    Scheduler orders priorities -> Worker fetch -> Execute

Autoscaling decision tree (simplified)

  if totalBacklog / BacklogPerWorkerScaleOut > currentWorkers -> Scale out
  else if idleWorkers > 0 and currentWorkers > MinWorkers -> Scale in

Aging growth curve (conceptual)

  effectiveWeight = baseWeight + ageSeconds * AgingFactor

NuGet Packaging Usage
- To pack:

  dotnet pack -c Release

- To publish:

  dotnet nuget push ./artifacts/*.nupkg --api-key <KEY> --source https://api.nuget.org/v3/index.json

Contributing
- Contributions welcome. Please open issues and PRs.

License
- MIT (placeholder)
