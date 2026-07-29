# Task 02 — ReaderWriterLock part + plain-lock contrast + throughput

## Goal
Add the readers/writer sub-system: N reader threads + 1 writer over a shared value using
`ReaderWriterLockSlim`, with a toggle to switch to a single `lock` (Monitor) that serializes readers,
and 1-second rolling reads/sec + writes/sec so the difference is visible.

## Traceability
- **requirements:** REQ-006, REQ-007, REQ-008 (rw part)
- **acceptance:** AC-004, AC-005

## Files allowed to touch
- Assets/Scripts/Scenarios/SyncPrimitivesDeadlockScenario.cs (extend)

## Acceptance
- [ ] N reader threads read a shared int under `ReaderWriterLockSlim.EnterReadLock`; 1 writer updates under `EnterWriteLock`.
- [ ] Counters via `Interlocked`: total reads, writes; 1-second rolling reads/sec + writes/sec.
- [ ] **RWLock ↔ plain lock** toggle: with a single `lock`, readers serialize and reads/sec drops clearly vs RWLock mode.
- [ ] Toggling the mode restarts the readers/writer cleanly (cancel + join before start).
- [ ] `ReaderWriterLockSlim` disposed in `Stop()` after joins; a lingering worker never touches a disposed lock (bind per-generation).
- [ ] Compiles clean (G2).
