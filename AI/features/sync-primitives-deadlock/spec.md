# Feature: Deadlock & ReaderWriterLock scenario

## Changelog
- **2026-07-24:** Initial spec.

## Problem
Two matrix ideas are still unshown: **deadlock** (the classic lock-ordering trap) and
**ReaderWriterLock(Slim)** (a slim primitive that lets many readers run concurrently but
serializes writers). A single scenario can make both visceral.

## Goal
A new `IThreadingScenario` — "Deadlock & ReaderWriterLock" — with two parts:
1. **Deadlock:** two worker threads take two locks in **opposite order** and get stuck; the UI
   shows each thread "holding X, waiting Y" and flags the deadlock. A **Fix (ordered locks)** toggle
   makes both take the locks in the **same order** → no deadlock, work proceeds.
2. **ReaderWriterLock:** many reader threads + one writer over a shared value using
   `ReaderWriterLockSlim`; show that readers run **concurrently** (high reads/sec) while writes are
   exclusive — vs a plain `lock` that serializes everything.

Matrix cells: synchronization primitives — **monitors, ReadWriteLocks and their slim counterparts** —
and **deadlock** (lock-ordering) with its fix.

## Non-goals
- No semaphores here (covered by Producer/Consumer #4) beyond a mention.
- No OS-level deadlock detection; "detected" = both threads stuck past a timeout.
- No `async` locks.

## Teardown-safety note (important)
A **true** hard deadlock cannot be joined on Exit (the threads are stuck forever). To stay
teardown-safe, the deadlock demo acquires the second lock with **`Monitor.TryEnter(lock, timeout)`
in a loop that also checks the CancellationToken** — so a "deadlocked" thread makes no progress
(shows the deadlock symptom) yet still exits promptly on cancel. The Fix path uses ordered
`lock`/`Monitor.Enter` and progresses normally.

## User scenarios
### Scenario A — Deadlock, then fix
1. User selects the scenario; the Deadlock part is running in "unordered" mode.
2. Within a moment both threads show "holding one lock, waiting for the other" and a **DEADLOCK**
   flag lights up; the progress counter stops.
3. User flips **Fix (ordered locks)** → both threads take locks in the same order, the deadlock
   clears, and the counter climbs again.

### Scenario B — Readers vs writer
1. Reader threads read a shared value continuously; a writer updates it occasionally.
2. With `ReaderWriterLockSlim`, reads/sec is high (readers overlap); toggling to a plain `lock`
   drops reads/sec sharply (everything serializes).

## Functional requirements

### REQ-001 — Scenario registered and selectable
`SyncPrimitivesDeadlockScenario : IThreadingScenario` under `Assets/Scripts/Scenarios/`, registered
in `ThreadingLabHost.BuildScenarios()`, appears in the picker.

### REQ-002 — Deadlock via opposite lock order
Two worker threads: A takes lock1 then lock2; B takes lock2 then lock1 (in unordered mode), each
holding the first briefly before requesting the second → they block each other.

### REQ-003 — Deadlock is teardown-safe
The second-lock acquisition uses `Monitor.TryEnter(lock, timeoutMs)` inside a loop that checks the
`CancellationToken`; a stuck thread exits promptly on cancel. No thread is left blocked after Exit.

### REQ-004 — Deadlock indicator + progress
Show each thread's state ("holding L1 / waiting L2", etc.), a **DEADLOCK** flag when both are stuck
past a short timeout, and a progress counter that only advances when a thread acquires both locks.

### REQ-005 — Fix (ordered locks)
A toggle makes both threads acquire the locks in the **same** order → no deadlock; the progress
counter climbs steadily.

### REQ-006 — ReaderWriterLockSlim readers/writer
N reader threads read a shared int under `ReaderWriterLockSlim.EnterReadLock`; one writer updates it
under `EnterWriteLock`. Counters: reads/sec, writes/sec.

### REQ-007 — Contrast with a plain lock
A toggle switches the readers/writer between `ReaderWriterLockSlim` and a single `lock` (Monitor);
with the plain lock, reads/sec drops sharply (readers serialize).

### REQ-008 — Lifecycle & cancellation
A `CancellationTokenSource` backs all worker threads (deadlock pair + readers + writer). `Exit()`
cancels and **joins** every thread — no leaked/stuck threads. The **Fix (ordered locks)** toggle
**restarts** the workers (needed to break an already-formed deadlock). The **lock-mode** toggle
(RWLock ↔ plain lock) is applied **live** (loops read the field each iteration) — no restart, so the
reads/sec change is instant; the brief switch window where threads still use the old lock is
intentional and benign (`_sharedValue` is an atomic int read into a discarded local).

### REQ-009 — No Unity API off the main thread
Workers touch only locks, `ReaderWriterLockSlim`, `Interlocked` counters, and the token. Counters
read on the main thread for display.

### REQ-010 — No per-frame churn
IMGUI styles cached; per-frame display reads simple counters.

## Failure scenarios
- Exit while deadlocked: the token releases the `TryEnter` loop; threads exit; no leak.
- Toggling Fix rapidly: each Fix toggle tears down the old workers (cancel+join) before starting new. The lock-mode toggle is live (no teardown); rapid toggling must not throw (benign transient only).

## Analytics / Data / persistence
- none.

## Platform constraints
- Editor / Standalone only. WebGL is out of scope (single-threaded). See `profile.yaml → platforms`.

## UX / UI
- Deadlock part: two thread-state lines, a DEADLOCK flag, a progress counter, a **Fix (ordered locks)** toggle.
- RWLock part: reads/sec, writes/sec, and a **ReaderWriterLockSlim ↔ plain lock** toggle.

## Acceptance criteria

### AC-001 — Selectable
- **Given** Play mode · **When** opening the picker · **Then** the scenario is listed and shows both parts.

### AC-002 — Deadlock occurs
- **Given** unordered mode · **When** the two threads run · **Then** both show waiting-for-the-other, the DEADLOCK flag lights, and the progress counter is stuck.

### AC-003 — Fix clears the deadlock
- **Given** the deadlock · **When** the user enables Fix (ordered locks) · **Then** the flag clears and the progress counter climbs.

### AC-004 — Readers overlap under RWLock
- **Given** ReaderWriterLockSlim mode · **When** readers run · **Then** reads/sec is high (readers concurrent) and writes still happen.

### AC-005 — Plain lock serializes
- **Given** the plain-lock toggle · **When** readers run · **Then** reads/sec drops clearly vs the RWLock mode.

### AC-006 — Clean teardown
- **Given** workers running (even deadlocked) · **When** leaving the scenario · **Then** all threads stop (process-thread count returns to baseline); nothing stuck.

### AC-007 — No cross-thread violations
- **Given** workers running · **When** they update state · **Then** no "main thread only" error and no unhandled exception in the Console.

## Open questions
- [ ] Deadlock second-lock TryEnter timeout: how long before flagging DEADLOCK. Recommended: request with a short poll (~50 ms) and flag "deadlock" after both wait > ~500 ms. Resolve at plan time.
- [ ] Reader count default. Recommended: 4 readers + 1 writer. Resolve at plan time.
