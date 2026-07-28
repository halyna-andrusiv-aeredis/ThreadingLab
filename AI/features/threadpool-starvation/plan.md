# Plan: ThreadPool Starvation scenario

Traces `spec.md` (REQ-001..011, AC-001..006). Stack per `AI/profile.yaml`: no DI, IMGUI, raw
`System.Threading`, QA target Editor/Standalone (not WebGL). Uses the shared `ThreadPool` (unlike #4's
dedicated `new Thread()` workers) — that shared, finite pool is the whole point.

## 1. Architecture proposal

One new scenario class + one registration line.

- **`ThreadPoolStarvationScenario : IThreadingScenario`** (new, `Assets/Scripts/Scenarios/`).
  - Counters (`Interlocked`/long): `_submitted`, `_started`, `_completed`.
  - `CancellationTokenSource _cts` (recreated on Enter; cancelled on Exit) backs the blocking waits.
  - Settings: `_burst` (items per submit), `_blockMs`, `_minThreads` (target `SetMinThreads` worker value).
  - Saved original min threads (`_origMinWorker`, `_origMinIo`) captured in `Enter` via `GetMinThreads`,
    restored in `Exit` via `SetMinThreads`.
  - **Submit():** for i in 0..burst: `ThreadPool.QueueUserWorkItem(WorkItem)`; `Interlocked.Add(ref _submitted, burst)`.
  - **WorkItem(state):** `Interlocked.Increment(ref _started)`; `_cts.Token.WaitHandle.WaitOne(_blockMs)`
    (blocks the pool thread — the starvation cause — but cancellable); `Interlocked.Increment(ref _completed)`.
  - **ApplyMinThreads():** `ThreadPool.SetMinThreads(_minThreads, ioMin)` — called when the control changes.
  - `Enter()`: capture original min, new CTS. `Exit()`: `_cts.Cancel()`, restore original min, dispose CTS.
  - `Tick()`: nothing heavy (pool getters are read in DrawGUI).
  - `DrawGUI`: counters + in-flight + backlog; a **backlog bar**; ThreadPool state (`ThreadCount`,
    `GetAvailableThreads`/`GetMaxThreads` → busy workers, `GetMinThreads`); steppers for burst, block ms,
    min threads; **Submit** + **Reset** buttons; help text (blocking vs async).

Resolved open questions: blocking-only demo + a help-text note that async avoids it; show both pool
thread count and busy-vs-min workers.

## 2. Dependency / impact analysis

- Depends on `IThreadingScenario` (stable). No `MainThreadDispatcher` (workers touch no Unity API; the
  main thread reads counters + pool getters).
- Blast radius: one additive `Add(...)` line. **Process-global caveat:** `SetMinThreads` affects the
  whole Editor/process — mitigated by capturing and restoring the original value on Enter/Exit.

## 3. Files to create / change

- **Create:** `Assets/Scripts/Scenarios/ThreadPoolStarvationScenario.cs`
- **Change:** `Assets/Scripts/Core/ThreadingLabHost.cs` (one registration line)

## 4. Risks

- **`SetMinThreads` is global and persists.** If not restored, it changes ThreadPool behavior for the
  whole Editor session (and other scenarios). Mitigation: capture `GetMinThreads` in `Enter`, restore in
  `Exit`; only raise it while active.
- **Fire-and-forget items outlive Exit.** In-flight blocking waits must be cancellable so `Exit` doesn't
  leave dozens of blocked pool threads. Mitigation: `WaitHandle.WaitOne(ms)` on the token → returns
  immediately on `Cancel()`. After cancel, items just increment `_completed` and exit.
- **Counter staleness across bursts.** Repeated submits accumulate; provide a **Reset** that clears
  counters (only when no items are in flight, or accept a transient).
- **Editor already has a warm pool.** `ThreadCount` starts non-trivial; headline the *delta* / backlog,
  not the absolute, so starvation reads clearly.

## 5. Step-by-step implementation plan (proposed task split)

### TASK_01 — Core burst + blocking pool work + counters + lifecycle
- **Goal:** `ThreadPoolStarvationScenario` registered; **Submit** queues a burst of `QueueUserWorkItem`;
  each item does a cancellable blocking wait; `Interlocked` counters (submitted/started/completed);
  `Enter` captures original min threads + new CTS; `Exit` cancels + restores min + disposes. Basic
  counter + ThreadPool-state display.
- **Files:** `Assets/Scripts/Scenarios/ThreadPoolStarvationScenario.cs` (new),
  `Assets/Scripts/Core/ThreadingLabHost.cs` (register).
- **Type:** Code
- **Expected result:** Submitting a big blocking burst shows Started plateau + slow Completed; leaving
  the scenario cancels in-flight waits and restores min threads.
- **Validation/check:** G2 compile; Play-mode: starvation visible, clean teardown, min restored.
- **Traceability:** REQ-001, 002, 003, 004, 005 (basic), 009, 010, 011; AC-001, 002 (basic), 005, 006.
- **Rollback risk:** Low (additive; one shell line). Note the global SetMinThreads restore.

### TASK_02 — Controls + backlog bar + mitigation + reset
- **Goal:** Steppers for burst size, block ms, min pool threads (`SetMinThreads`); backlog bar; a Reset
  button; the mitigation path (raise min → next burst doesn't starve).
- **Files:** `Assets/Scripts/Scenarios/ThreadPoolStarvationScenario.cs` (extend).
- **Type:** Code
- **Expected result:** Default min → starvation (high backlog, creeping thread count); raised min → burst
  runs at once, backlog ~0.
- **Validation/check:** G2 compile; Play-mode: exercise starve vs mitigate.
- **Traceability:** REQ-006, 007, 008; AC-003, 004.
- **Rollback risk:** Low (same new file).

### TASK_03 — Manual QA (validation)
- **Goal:** Verify AC-001..006 (starvation, mitigation, counter consistency, clean teardown + min
  restore, no cross-thread errors).
- **Files:** none (manual).
- **Type:** validation
- **Traceability:** AC-001..006.
- **Rollback risk:** n/a.
