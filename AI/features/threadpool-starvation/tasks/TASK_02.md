# Task 02 — Controls + backlog bar + mitigation (SetMinThreads) + reset

## Goal
Add steppers for burst size, block ms, and **min pool threads** (`SetMinThreads`), a backlog bar, and a
Reset button — so the user can trigger starvation and then mitigate it by raising the pool minimum.

## Traceability
- **requirements:** REQ-006, REQ-007, REQ-008
- **acceptance:** AC-003, AC-004

## Files allowed to touch
- Assets/Scripts/Scenarios/ThreadPoolStarvationScenario.cs (extend)

## Acceptance
- [ ] Steppers for burst size, block ms, and min pool threads; changing min threads calls `ThreadPool.SetMinThreads` (worker count; keep the IO min unchanged).
- [ ] Backlog bar drawn (submitted − started vs a sensible scale).
- [ ] Reset button clears the counters.
- [ ] Default min → submitting a blocking burst starves: high backlog, Started ≈ core count, ThreadPool thread count creeps up.
- [ ] Raised min → the same burst starts nearly all items at once, backlog ~0, completes quickly.
- [ ] The original min threads is still restored on Exit (from TASK_01), even after the user changed it.
- [ ] Compiles clean (G2).
