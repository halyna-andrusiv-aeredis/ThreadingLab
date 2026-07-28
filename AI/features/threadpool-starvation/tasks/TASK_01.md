# Task 01 — Core burst + blocking pool work + counters + lifecycle

## Goal
Create `ThreadPoolStarvationScenario`, register it, and implement a **Submit** that queues a burst of
`ThreadPool.QueueUserWorkItem`, each item doing a **cancellable blocking wait** (`ct.WaitHandle.WaitOne(blockMs)`)
that holds a pool thread. `Interlocked` counters (submitted/started/completed). `Enter` captures the
original `GetMinThreads` and a fresh CTS; `Exit` cancels and **restores** the original min threads.
Basic counter + ThreadPool-state display.

## Traceability
- **requirements:** REQ-001, REQ-002, REQ-003, REQ-004, REQ-005 (basic), REQ-009, REQ-010, REQ-011
- **acceptance:** AC-001, AC-002 (basic), AC-005, AC-006

## Files allowed to touch
- Assets/Scripts/Scenarios/ThreadPoolStarvationScenario.cs (new)
- Assets/Scripts/Core/ThreadingLabHost.cs (add one registration line in BuildScenarios)

## Acceptance
- [ ] Scenario appears in the picker and is selectable.
- [ ] **Submit** queues a burst of work items via `ThreadPool.QueueUserWorkItem`.
- [ ] Each item: `Interlocked.Increment(_started)` → `_cts.Token.WaitHandle.WaitOne(blockMs)` (blocks the pool thread, cancellable) → `Interlocked.Increment(_completed)`.
- [ ] Counters via `Interlocked`: submitted, started, completed; in-flight and backlog derived.
- [ ] With a big block ms + burst above core count, Started plateaus near core count and Completed rises slowly (starvation visible).
- [ ] `Enter` captures original `GetMinThreads`; `Exit` cancels the CTS (in-flight waits return promptly) and **restores** the original min threads. No runaway threads.
- [ ] Workers touch no `UnityEngine` API; counters/pool state read on the main thread. Cached IMGUI styles.
- [ ] Compiles clean (G2).
