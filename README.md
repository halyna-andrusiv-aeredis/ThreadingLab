# Threading Lab

Interactive Unity lab for **seeing** multithreading: each scenario runs the same task
"with threads / without threads / with different sync", and visualizes what happens on the
main thread and worker threads in real time.

Unity **6000.0.66f2 LTS**. Pure `System.Threading` / TPL — no external packages.

## How to run

1. Open the folder `C:\Projects\ThreadingLab` in Unity Hub (Unity 6000.0.66f2).
2. Press **Play**. The lab UI appears automatically (a bootstrapper spawns it — no scene setup needed).
3. Pick a scenario on the left; use the buttons; watch the FPS / thread strip at the top.

## Architecture (built to grow)

- `Core/IThreadingScenario.cs` — one demo = one implementation of this interface.
- `Core/ThreadingLabHost.cs` — the shell: scenario picker + metrics strip. Drives the active scenario.
- `Core/MainThreadDispatcher.cs` — marshals worker-thread results back to the Unity main thread.
- `Core/FpsMeter.cs` — rolling FPS for the metrics strip.
- `Core/Bootstrap.cs` — auto-spawns the host on Play.
- `Scenarios/` — the demos.

### Adding a new demo
Implement `IThreadingScenario`, then add it in `ThreadingLabHost.BuildScenarios()`. That's it.

## Scenarios & the competency matrix

| # | Scenario | Matrix cells |
|---|----------|--------------|
| 1 | Race Condition | sync primitives (lock/Monitor), atomic operations (Interlocked) | ✅ done |
| — | Main Thread Freeze | Thread class, main-thread marshaling | planned |
| — | Sequential vs ThreadPool vs Parallel.For | ThreadPool, parallelism | planned |
| — | Deadlock (wait-for graph) | lock ordering, deadlock | planned |
| — | Producer / Consumer | ConcurrentQueue, SemaphoreSlim | planned |
| — | async / SynchronizationContext | custom awaitable, SynchronizationContext | planned |
