# Task 03 — Manual QA (validation)

## Goal
Verify the scenario in Play mode: deadlock occurs and the Fix clears it; readers overlap under
ReaderWriterLockSlim vs serialize under a plain lock; clean teardown even when deadlocked; no
cross-thread Console errors.

## Traceability
- **acceptance:** AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007

## Files allowed to touch
- (none — manual QA)

## Acceptance
- [ ] AC-001 — selectable in the picker; both parts visible.
- [ ] AC-002 — unordered mode: both threads wait for the other, DEADLOCK flag lights, progress stuck.
- [ ] AC-003 — Fix (ordered locks): flag clears, progress climbs.
- [ ] AC-004 — ReaderWriterLockSlim: reads/sec high (readers overlap); writes happen.
- [ ] AC-005 — plain-lock toggle: reads/sec drops clearly (readers serialize).
- [ ] AC-006 — leaving the scenario (even while deadlocked) stops all threads; process-thread count returns to baseline.
- [ ] AC-007 — no cross-thread errors/exceptions in the Console.
