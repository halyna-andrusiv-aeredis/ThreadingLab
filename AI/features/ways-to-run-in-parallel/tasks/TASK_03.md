# Task 03 — Manual QA (validation)

## Goal
Verify the scenario in Play mode against the acceptance criteria: the creature animates,
Sequential is the slow baseline, the parallel methods speed it up, the image is identical across
methods, there are no cross-thread Console errors, and nothing leaks on re-entry.

## Traceability
- **acceptance:** AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007, AC-008

## Files allowed to touch
- (none — manual QA)

## Acceptance
- [ ] AC-001 — selectable in the picker.
- [ ] AC-002 — the red creature/field visibly pulsates.
- [ ] AC-003 — Sequential at high weight: low FPS / high compute-ms.
- [ ] AC-004 — Parallel.For: noticeably lower compute-ms / higher FPS at the same weight.
- [ ] AC-005 — ThreadPool: similar speedup to Parallel.For.
- [ ] AC-006 — switching method keeps the image identical; metrics update live; no restart.
- [ ] AC-007 — no cross-thread errors/exceptions in the Console.
- [ ] AC-008 — switching away and back several times does not grow texture/thread counts.
