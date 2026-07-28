# Feature: ThreadPool Starvation scenario

## Changelog
- **2026-07-24:** Initial spec.

## Problem
"ThreadPool starvation" is the dangerous cousin of the idle-consumer starvation in scenario #4, and
it is a real matrix competency. It needs its own scenario: block the shared `ThreadPool`'s workers and
watch queued work fail to run while the pool only slowly injects new threads.

## Goal
A new `IThreadingScenario` — "ThreadPool Starvation" — that submits a burst of work items to
`ThreadPool.QueueUserWorkItem`, where each item **blocks** its pool thread for a while. With the
default pool minimum, the burst starves: only ~core-count items start immediately, the rest sit in the
pool's queue (backlog), completions trickle, and `ThreadPool.ThreadCount` creeps up ~1/sec. Raising the
pool minimum (`SetMinThreads`) — the standard mitigation — lets the next burst run without starving.

Matrix cell: **basic understanding of ThreadPool starvation** (and its cause: blocking pool threads).

## Non-goals
- No `async/await` alternative implementation (mention in the help text that non-blocking async avoids
  the problem, but the demo is about the blocking failure mode).
- Not a general scheduler; no work-stealing internals.
- `SetMinThreads` is process-global — the scenario changes it only while active and restores it on Exit;
  it does not try to sandbox the pool.

## User scenarios
### Scenario A — Starve, then fix
1. User selects "ThreadPool Starvation". Sets a big block time and a large burst, presses **Submit burst**.
2. Started plateaus at ~core count, **backlog** (submitted − started) stays high, Completed trickles,
   and **pool thread count** slowly creeps upward — the pool is starved.
3. User raises **Min pool threads** (SetMinThreads) and submits another burst → nearly all items start
   at once, backlog ~0, the burst completes quickly.

## Functional requirements

### REQ-001 — Scenario registered and selectable
`ThreadPoolStarvationScenario : IThreadingScenario` under `Assets/Scripts/Scenarios/`, registered in
`ThreadingLabHost.BuildScenarios()`, appears in the picker.

### REQ-002 — Submit a burst to the ThreadPool
A "Submit burst" action queues N work items via `ThreadPool.QueueUserWorkItem`.

### REQ-003 — Blocking work items (the starvation cause)
Each work item **blocks its pool thread** for `blockMs` using a cancellable blocking wait
(`ct.WaitHandle.WaitOne(blockMs)` — holds the thread like real blocking work, but returns promptly on
cancel), then marks itself complete.

### REQ-004 — Counters
`Interlocked` counters for submitted, started, completed; derive in-flight (started − completed) and
backlog (submitted − started).

### REQ-005 — Live ThreadPool state
Show **busy worker threads** as `GetMaxThreads − GetAvailableThreads` (which creeps up as the pool
injects threads — the starvation signal) and the current min worker threads (`GetMinThreads`).
NOTE: deliberately avoid `ThreadPool.ThreadCount`/`PendingWorkItemCount` — .NET Core 3.0+ members not
reliably present on Unity's Mono/IL2CPP BCL; the busy-workers proxy + the top-strip process-thread
count cover it safely.

### REQ-006 — Controls
Adjustable burst size, block ms, and **min pool threads** (`SetMinThreads`, worker threads) so the user
can trigger starvation and then mitigate it.

### REQ-007 — Starvation visible (default min)
With the default min and a blocking burst, started plateaus near core count, backlog stays high,
completed rises slowly, and `ThreadPool.ThreadCount` creeps up ~1/sec.

### REQ-008 — Mitigation visible
Raising min pool threads (`SetMinThreads`) before a burst lets nearly all items start immediately and
the backlog clear quickly.

### REQ-009 — Lifecycle & cancellation
A `CancellationTokenSource` backs the blocking waits. `Exit()` cancels (in-flight items return promptly,
no runaway) and **restores the original `SetMinThreads`** value it changed.

### REQ-010 — No Unity API off the main thread
Work items touch only `Interlocked` counters, the token wait-handle, and `ThreadPool` APIs. Counters and
pool state are read on the main thread for display.

### REQ-011 — No per-frame churn
IMGUI styles cached; per-frame display reads counters + a few `ThreadPool` getters (cheap).

## Failure scenarios
- Exit while items are blocked: the token releases every `WaitHandle.WaitOne`, items complete, threads
  free — no runaway; original min threads restored.
- Re-submitting bursts repeatedly: counters accumulate sensibly (or a Reset control clears them).

## Analytics
- none.

## Data / persistence
- none.

## Platform constraints
- Editor / Standalone only. WebGL is out of scope (single-threaded; no real ThreadPool). See `profile.yaml → platforms`.
- `SetMinThreads` affects the whole process — restored on Exit (REQ-009).

## UX / UI
- Counters (submitted/started/completed, in-flight, backlog); ThreadPool state (thread count, available,
  min); a backlog bar; controls for burst size, block ms, min pool threads; Submit + Reset buttons.

## Acceptance criteria

### AC-001 — Selectable
- **Given** Play mode
- **When** the user opens the picker
- **Then** "ThreadPool Starvation" is listed and selecting it shows the counters + ThreadPool state + controls.

### AC-002 — Starvation (default min)
- **Given** a large block ms and a burst well above core count, with default min threads
- **When** the user submits the burst
- **Then** Started plateaus near core count, backlog (submitted − started) stays high, Completed rises slowly, and ThreadPool thread count creeps up over seconds.

### AC-003 — Mitigation
- **Given** min pool threads raised high (SetMinThreads) with the same burst
- **When** the user submits
- **Then** nearly all items start almost immediately, backlog drops to ~0, and the burst completes quickly.

### AC-004 — Counters consistent
- **Given** a running burst
- **When** watching the counters
- **Then** completed ≤ started ≤ submitted, in-flight = started − completed, backlog = submitted − started.

### AC-005 — Clean teardown
- **Given** items are blocked in-flight
- **When** the user switches to another scenario
- **Then** the token cancels the waits (no runaway; ThreadPool thread count settles) and the original min threads value is restored.

### AC-006 — No cross-thread violations
- **Given** work items running
- **When** they update counters
- **Then** no "can only be called from the main thread" error and no unhandled exception in the Console.

## Open questions
- [ ] Show a "blocking vs non-blocking" toggle (Thread-blocking WaitOne vs a short async delay) or keep
      the demo blocking-only. Recommended: blocking-only + a help-text note that async avoids it. Resolve at plan time.
- [ ] Which pool metric headline: `ThreadCount` vs busy-worker count (`GetMaxThreads − GetAvailableThreads`).
      Recommended: show both, headline the busy-vs-min relationship. Resolve at plan time.
