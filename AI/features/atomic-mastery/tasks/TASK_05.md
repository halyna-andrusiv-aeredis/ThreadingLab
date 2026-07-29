# Task 05 — Manual QA (validation)

## Goal
Verify the whole feature in Play mode against the acceptance criteria: scenario selectable; Mode A
correctness + lock-free throughput advantage; Mode B CAS retries + conservation invariant; Mode C
false-sharing cliff; clean teardown (all threads joined on mode switch / Exit); no cross-thread errors.

## Traceability
- **acceptance:** AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007

## Files allowed to touch
- none (manual QA in Unity)

## Acceptance
- [ ] AC-001 — "6 · Atomic Mastery" listed; selecting shows mode selector + controls.
- [ ] AC-002 — Mode A: all three strategies' final count equals expected total.
- [ ] AC-003 — Mode A: Interlocked & CAS-loop ops/sec > lock baseline at high thread count.
- [ ] AC-004 — Mode B: CAS retries > 0 under contention; `pushed = popped + remaining` holds.
- [ ] AC-005 — Mode C: padded total ops/sec noticeably higher than packed.
- [ ] AC-006 — Switching mode / leaving mid-run joins all workers; top-strip thread count settles (no runaway).
- [ ] AC-007 — No "can only be called from the main thread" error, no unhandled exception in the Console.
