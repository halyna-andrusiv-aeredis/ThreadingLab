# Task 02 — Background (non-freezing) path + marshaling + min-FPS

## Goal
Add the "Run on Background" path to `MainThreadFreezeScenario`: the same workload via
`Task.Run`, result marshaled back through `MainThreadDispatcher`, min-FPS-during-run tracked
in `Tick`, and a guarded late callback so `Exit()` leaves no leaked worker.

## Traceability
- **requirements:** REQ-004, REQ-005, REQ-008 (background half + min-FPS), REQ-009
- **acceptance:** AC-004, AC-006, AC-007

## Files allowed to touch
- Assets/Scripts/Scenarios/MainThreadFreezeScenario.cs (extend)

## Acceptance
- [ ] "Run on Background" runs the same `Workload()` via `Task.Run` (off the main thread).
- [ ] Result is written to UI state only via `MainThreadDispatcher.Enqueue(...)`.
- [ ] While running in background, the indicator keeps animating and FPS stays high.
- [ ] `Tick` tracks the worst FPS observed during a run; it is shown after completion.
- [ ] No "can only be called from the main thread" error or unhandled exception in the Console.
- [ ] Switching scenario mid-run leaves no stuck worker (guarded flag; thread count returns to baseline).
- [ ] Compiles clean (G2).
