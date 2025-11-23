<!-- .github/copilot-instructions.md - Guidance for AI coding agents working on this repo -->
# Copilot / AI Agent Instructions — Console-Sandbox

Purpose: short, actionable guidance to help an AI agent be immediately productive in this repository.

- **Target framework:
** `net10.0` 
Projects use SDK-style csproj with implicit file inclusion — add `.cs` files under the project folder without editing the csproj.

- **Big picture architecture:**
  - The repo is a sandbox of console demos (not a multi-project service). The primary example implements an auto-scaling worker pool that:
    - Uses one or more `Channel<Func<Task>>` per priority (see `PriorityQueues.cs`)
    - Runs a pool of worker tasks that pull highest-priority work first (`WorkerLoop`)
    - Uses an `AutoscalerLoop` that inspects backlog metrics and calls `AddWorker` / `RemoveWorker` with cooldowns
  - The `MinimalExample.cs` demonstrates a simpler single-channel scaler; `PriorityQueues.cs` shows the production-ready pattern.

- **Project-specific conventions & patterns to follow:**
  - Top-level statements are used for small demos
  - Best practices separation of concerns for scaling logic into classes/methods and project folder organization.
  - Cancellation model: use `CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token)` to create worker-scoped tokens.
  - Channel pattern: write producers with `ChannelWriter<Func<Task>>` and workers execute `Func<Task>` delegates.
  - Scaling heuristics: backlog is measured via `channel.Reader.Count` and compared against `BacklogPerWorkerScaleOut`; `ScaleCooldown` guards rapid oscillation.
  - Logging: demos use `Console.WriteLine` for observability.

