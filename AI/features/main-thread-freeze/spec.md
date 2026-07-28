# Feature: Main Thread Freeze scenario

## Changelog
- **2026-07-24:** Initial spec.

## Problem
A newcomer to threading cannot *see* why heavy work must leave the main thread. The lab
needs a scenario that makes the freeze visceral: run the same workload on the main thread
(everything locks up) versus a background thread (the app stays alive).

## Goal
A new `IThreadingScenario` — "Main Thread Freeze" — with an always-animating liveness
indicator and two buttons ("Run on Main Thread" / "Run on Background"). Running on the main
thread visibly freezes the indicator and drops FPS to ~0 for the duration; running in the
background keeps it smooth. This demonstrates the matrix cells: **Thread class / running code
on background threads** and **marshaling results back to the main thread**.

## Non-goals
- No `CancellationToken` / cancel button (that is a later scenario).
- No comparison across Thread vs ThreadPool vs Parallel.For (that is scenario "Ways to run in parallel").
- No new shell/infrastructure changes beyond using the existing `MainThreadDispatcher`.
- Not tuning or measuring GC; only wall-clock time + FPS are shown.

## User scenarios
### Scenario A — Feel the freeze
1. User selects "Main Thread Freeze" in the picker.
2. A spinner (or moving bar) is animating; the top-strip FPS reads the normal rate.
3. User clicks **Run on Main Thread** → the spinner stops dead, FPS reads ~0, the window is
   unresponsive for ~2–3 s, then it snaps back and shows the result + elapsed time.
4. User clicks **Run on Background** → the spinner keeps spinning, FPS stays high, and the
   result appears when the background work finishes.

## Functional requirements

### REQ-001 — Scenario registered and selectable
A `MainThreadFreezeScenario` implementing `IThreadingScenario` exists under
`Assets/Scripts/Scenarios/` and is registered in `ThreadingLabHost.BuildScenarios()` so it
appears in the picker.

### REQ-002 — Always-animating liveness indicator
The scenario draws an indicator whose animation is driven from `Tick(deltaTime)` /
`Time.unscaledTime` so that it *only* keeps moving while the main thread runs frames. This is
the visual proof of freeze vs. no-freeze.

### REQ-003 — Run on main thread (blocking)
A "Run on Main Thread" button executes the heavy workload **synchronously on the main thread**
(inside the button handler), deliberately blocking the frame loop for the duration.

### REQ-004 — Run on background thread (non-blocking)
A "Run on Background" button executes the **same** workload via `Task.Run` (off the main
thread), leaving the frame loop free to keep rendering.

### REQ-005 — Marshal results to the main thread
The background run publishes its result to the scenario's UI state **only** via
`MainThreadDispatcher.Enqueue(...)`. No Unity API is touched from the worker thread.

### REQ-006 — Deterministic, equal workload
Both modes run the identical workload (same fixed input, e.g. a busy compute of fixed size),
so the two elapsed times and outcomes are directly comparable.

### REQ-007 — No re-entrancy
While a run (either mode) is in progress, both run buttons are disabled so a second run cannot
start until the current one finishes.

### REQ-008 — Result & metrics display
After a run, the scenario shows: mode used, the computed result, elapsed wall-clock time (ms),
and the minimum FPS observed during the run.

### REQ-009 — Clean teardown
`Exit()` must not leave a running/leaked worker. The background workload is short-lived and
self-completing; a late dispatcher callback after `Exit()` must be harmless (guarded flag).

### REQ-010 — No per-frame allocations in DrawGUI
IMGUI styles are built once (cached), consistent with the project rule on `DrawGUI` allocations
(so the scenario itself does not perturb the FPS metric it displays).

## Failure scenarios
- Background task throws → the exception is surfaced (logged / shown), not silently swallowed (REQ-005 path still completes the "running" flag).
- User switches scenario mid-run → `Exit()` is called; the in-flight task completes in the background and its callback is ignored (guarded); no stuck worker (thread count returns to baseline).

## Analytics
- none.

## Data / persistence
- none.

## Platform constraints
- Editor / Standalone only. WebGL is out of scope (single-threaded — the background mode cannot run there). See `AI/profile.yaml → platforms`.

## UX / UI
- Idle: animating indicator + two enabled buttons + hint text.
- Running: buttons disabled; status shows "running…"; on main-thread mode the indicator is frozen by definition.
- Done: mode, result, elapsed ms, min-FPS-during-run.

## Acceptance criteria

### AC-001 — Selectable
- **Given** the lab is in Play mode
- **When** the user opens the scenario picker
- **Then** "Main Thread Freeze" is listed and selecting it shows its controls.

### AC-002 — Idle liveness
- **Given** the scenario is active and idle
- **When** no run is triggered
- **Then** the indicator animates continuously and the top-strip FPS reads the normal frame rate.

### AC-003 — Main-thread run freezes
- **Given** the scenario is idle
- **When** the user clicks "Run on Main Thread"
- **Then** the indicator visibly stops, FPS drops to ~0 for the run duration, the window is unresponsive, and afterward the result + elapsed time appear.

### AC-004 — Background run stays smooth
- **Given** the scenario is idle
- **When** the user clicks "Run on Background"
- **Then** the indicator keeps animating, FPS stays high, and the result + elapsed time appear when the work completes.

### AC-005 — No re-entrancy
- **Given** a run is in progress
- **When** the user tries to click a run button
- **Then** both run buttons are disabled and no second run starts.

### AC-006 — No cross-thread violations
- **Given** a background run
- **When** it completes and publishes its result
- **Then** no "can only be called from the main thread" error or unhandled exception appears in the Console.

### AC-007 — No leaked worker
- **Given** a background run is in progress
- **When** the user switches to another scenario and back
- **Then** no stuck worker remains (process-thread count returns to baseline) and the UI is consistent.

## Open questions
- [ ] Workload shape: busy-spin for a fixed ~2.5 s vs. a fixed-size deterministic compute (e.g. count primes ≤ N). Recommended: fixed-size deterministic compute, so both modes produce the same result and elapsed time is meaningful. (Resolve at plan time.)
- [ ] Indicator form: rotating tick / sweeping bar / incrementing counter. Recommended: a sweeping bar driven by `Time.unscaledTime` (cheap, unmistakably frozen when blocked).
