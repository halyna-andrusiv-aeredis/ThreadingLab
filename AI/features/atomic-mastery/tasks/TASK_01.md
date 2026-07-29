# Task 01 — Scenario skeleton, registration, run harness, lifecycle

## Goal
Create `AtomicMasteryScenario : IThreadingScenario` (title "6 · Atomic Mastery"), register it in
`ThreadingLabHost.BuildScenarios()`, and build the shared plumbing every mode reuses: a **mode enum**
`{ LockFreeCounter, TreiberStack, FalseSharing }` + segmented selector; common controls (thread count,
run duration ms, **Run**, **Reset**) via the existing `Stepper` pattern; a **timed run harness** that
spawns N `new Thread`, bounds them by a token/duration, and **joins all threads** before the run is
considered finished; an `_isRunning` guard (one run at a time). `Enter()` initializes settings/CTS;
`Exit()` cancels the active run and **joins any live threads unconditionally**; switching mode also
stops-and-joins first. Mode bodies are wired but may be empty placeholders (filled in TASK_02..04).
Cached IMGUI styles (`EnsureStyles`).

## Traceability
- **requirements:** REQ-001, REQ-002, REQ-003, REQ-012, REQ-013, REQ-014, REQ-015
- **acceptance:** AC-001, AC-006, AC-007

## Files allowed to touch
- Assets/Scripts/Scenarios/AtomicMasteryScenario.cs (new)
- Assets/Scripts/Core/ThreadingLabHost.cs (add one registration line in BuildScenarios)

## Acceptance
- [ ] Scenario "6 · Atomic Mastery" appears in the picker and is selectable.
- [ ] Mode selector switches between LockFreeCounter / TreiberStack / FalseSharing; switching stops+joins any live run first.
- [ ] Controls: thread count and run duration (ms) via `Stepper`; **Run** starts a run, **Reset** clears last results (no-op while running).
- [ ] Run harness spawns N `new Thread`, bounds work by token/duration, and **joins every thread** before the run finishes; `_isRunning` prevents overlapping runs.
- [ ] `Enter` inits state/CTS; `Exit` cancels and **joins all live threads** — leaving the scenario mid-run leaves no runaway worker (top-strip thread count settles).
- [ ] Workers touch no `UnityEngine` API; all state read on the main thread. Cached IMGUI styles (built once).
- [ ] Compiles clean (G2).
