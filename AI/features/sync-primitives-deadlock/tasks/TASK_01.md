# Task 01 — Deadlock part (occur + fix) + lifecycle

## Goal
Create `SyncPrimitivesDeadlockScenario`, register it, and implement the deadlock demo: two worker
threads take two locks in opposite order and get stuck (teardown-safe via `Monitor.TryEnter` + token),
with per-thread state, a DEADLOCK flag, a progress counter, and a **Fix (ordered locks)** toggle.
`Start`/`Stop` (cancel + join) wired to `Enter`/`Exit`.

## Traceability
- **requirements:** REQ-001, REQ-002, REQ-003, REQ-004, REQ-005, REQ-008 (deadlock part), REQ-009, REQ-010
- **acceptance:** AC-001, AC-002, AC-003, AC-006, AC-007

## Files allowed to touch
- Assets/Scripts/Scenarios/SyncPrimitivesDeadlockScenario.cs (new)
- Assets/Scripts/Core/ThreadingLabHost.cs (add one registration line in BuildScenarios)

## Acceptance
- [ ] Scenario appears in the picker and is selectable.
- [ ] Two threads: unordered mode → A takes lock1→lock2, B takes lock2→lock1 → mutual block.
- [ ] Second lock acquired via `Monitor.TryEnter(second, pollMs)` in a loop that checks the token (teardown-safe).
- [ ] Per-thread state shown ("holding L1 / waiting L2"); a DEADLOCK flag lights when both wait past ~500 ms; a progress counter advances only when a thread gets both locks.
- [ ] **Fix (ordered locks)** toggle → both take locks in the same order → no deadlock; progress climbs (Restart on toggle: cancel+join then start).
- [ ] `Enter` starts the threads; `Exit` cancels + joins — no leaked/stuck thread even when "deadlocked".
- [ ] Workers touch no `UnityEngine` API; counters via `Interlocked`; cached IMGUI styles.
- [ ] Compiles clean (G2).
