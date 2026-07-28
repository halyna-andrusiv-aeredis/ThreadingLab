# Threading Lab — project context

## What it is
An interactive Unity **visualizer of multithreading**. Each "scenario" runs the same
workload in different ways (main thread / `Thread` / `ThreadPool` / `Task.Run` /
`Parallel.For`, and with `lock` / `Interlocked` / `SemaphoreSlim` / `ConcurrentQueue`)
and shows — in real time — what happens on the main thread and worker threads: FPS
drops, lost updates in a race, deadlock, ThreadPool starvation, etc.

Goal: a portfolio piece proving professional-level .NET concurrency for Unity. Mapped
to a competency matrix (Thread, ThreadPool + starvation, sync primitives, atomic
operations, concurrent collections, SynchronizationContext, custom awaitable / Task-like).

**Explicitly out of scope:** Unity Job System / Burst / `NativeArray` — the target
matrix is plain `System.Threading` / TPL, so the primitives stay visible.

## Stack
- Unity **6000.0.66f2 LTS**, Built-in pipeline, C#.
- **No** DI, UniTask, UniRx, Addressables. Pure `System.Threading` + TPL.
- UI is **IMGUI** (`OnGUI`) — drawn entirely in code, so there is no scene/prefab wiring.
- See `AI/profile.yaml` for the authoritative stack slots.

## Entry point / composition
- `Assets/Scripts/Core/Bootstrap.cs` — `[RuntimeInitializeOnLoadMethod]` auto-spawns
  the host on Play. No scene setup needed.
- `Assets/Scripts/Core/ThreadingLabHost.cs` — the shell: scenario picker + metrics
  strip (FPS / min FPS / thread count). Owns the `IThreadingScenario` list and drives
  the active one.
- `Assets/Scripts/Core/MainThreadDispatcher.cs` — marshals worker-thread results back
  to the Unity main thread (the core cross-thread rule of the whole project).
- `Assets/Scripts/Core/FpsMeter.cs` — rolling FPS for the metrics strip.

## Adding a scenario
Implement `Assets/Scripts/Core/IThreadingScenario.cs`
(`Title`, `Description`, `Enter`, `Exit`, `Tick`, `DrawGUI`) and register it in
`ThreadingLabHost.BuildScenarios()`. Each scenario MUST stop/join its threads in `Exit()`.

## Folders
- `Assets/Scripts/Core/` — shell + infrastructure.
- `Assets/Scripts/Scenarios/` — the demos (one file per scenario).

## Status
- Done: `RaceConditionScenario` (lock vs Interlocked vs no-sync).
- Planned: Main-thread freeze, Sequential vs ThreadPool vs Parallel.For, Deadlock
  (wait-for graph), Producer/Consumer, async + SynchronizationContext, custom awaitable.
