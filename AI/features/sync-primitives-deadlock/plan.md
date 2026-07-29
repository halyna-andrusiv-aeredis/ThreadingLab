# Plan: Deadlock & ReaderWriterLock scenario

Traces `spec.md` (REQ-001..010, AC-001..007). Stack per `AI/profile.yaml`: no DI, IMGUI, raw
`System.Threading`, QA target Editor/Standalone (not WebGL). Long-lived worker threads — lifecycle +
cancellation are the discipline (as in Producer/Consumer).

## 1. Architecture proposal

One new scenario class + one registration line. Two independent sub-systems inside it.

- **`SyncPrimitivesDeadlockScenario : IThreadingScenario`** (new, `Assets/Scripts/Scenarios/`).
  - Shared: `CancellationTokenSource _cts`, `List<Thread> _threads`, cached IMGUI styles.
  - **Deadlock part:**
    - `object _lockA, _lockB`; two threads. Mode `_ordered` (the Fix toggle).
    - Each thread: take its first lock (`Monitor.Enter`), briefly hold, then acquire the second with
      `Monitor.TryEnter(second, pollMs)` in a loop that checks the token (teardown-safe, REQ-003).
      On success: `Interlocked.Increment(_progress)`, release both, repeat. In **unordered** mode A
      wants (A→B), B wants (B→A) → mutual block; in **ordered** mode both use (A→B) → no deadlock.
    - State per thread (`volatile` string/enum): "holding L1, waiting L2" etc.; a **deadlock flag**
      = both threads have been waiting for their second lock past `deadlockMs`.
  - **RWLock part:**
    - `ReaderWriterLockSlim _rw`; a shared `int _value`; mode `_useRwLock` (toggle vs a plain `object _plainLock`).
    - Reader threads loop: read the value under a read lock (or the plain lock), `Interlocked.Increment(_reads)`.
    - One writer loops: write under a write lock (or the plain lock), `Interlocked.Increment(_writes)`, small delay.
    - 1-second rolling reads/sec + writes/sec (sampled in `Tick`).
  - **Lifecycle:** `Start()` builds the `_cts` + all threads; `Stop()` cancels + **joins** all
    (the TryEnter loop + read/write loops all observe the token, so joins return promptly); `Restart()`
    = Stop+Start (used by the Fix and lock-mode toggles). `Enter`→Start, `Exit`→Stop.
  - `Tick`: update the rolling rate samplers + the deadlock-flag timing. `DrawGUI`: two blocks + toggles.

Resolved open questions: TryEnter poll ~50 ms, deadlock flag after both wait > ~500 ms; 4 readers + 1 writer.

## 2. Dependency / impact analysis

- Depends on `IThreadingScenario` (stable). No `MainThreadDispatcher` (workers touch no Unity API).
- Blast radius: one additive `Add(...)` line in the shell. All state instance-scoped. Long-lived
  threads → teardown correctness (esp. the deadlock pair) is the main risk.

## 3. Files to create / change

- **Create:** `Assets/Scripts/Scenarios/SyncPrimitivesDeadlockScenario.cs`
- **Change:** `Assets/Scripts/Core/ThreadingLabHost.cs` (one registration line)

## 4. Risks

- **Un-joinable deadlock on teardown.** A real hard deadlock can't be joined. Mitigation: second lock
  via `Monitor.TryEnter(timeout)` + token check (REQ-003), so "deadlocked" threads still exit on cancel.
- **Toggle races.** Fix / lock-mode toggles must fully Stop (cancel+join) before Start. Mitigation:
  `Restart()` tears down first; toggles run on the main thread (DrawGUI), sequential.
- **ReaderWriterLockSlim disposal.** Dispose it in `Stop()` after joins; a lingering worker must not
  touch a disposed lock. Mitigation: join before dispose; bind per-generation like Producer/Consumer.
- **Deadlock-flag flicker.** Use a stable timeout (both waiting > ~500 ms) so the flag doesn't blink.

## 5. Step-by-step implementation plan (proposed task split)

### TASK_01 — Deadlock part (occur + fix) + lifecycle
- **Goal:** Scenario registered; two-thread deadlock via opposite lock order; teardown-safe TryEnter+token;
  per-thread state + DEADLOCK flag + progress counter; **Fix (ordered locks)** toggle; `Start/Stop`
  (cancel+join) wired to `Enter/Exit`.
- **Files:** `Assets/Scripts/Scenarios/SyncPrimitivesDeadlockScenario.cs` (new), `ThreadingLabHost.cs` (register).
- **Type:** Code · **Traceability:** REQ-001,002,003,004,005,008 (deadlock part),009,010; AC-001,002,003,006,007.
- **Rollback risk:** Low (additive; one shell line).

### TASK_02 — ReaderWriterLock part + plain-lock contrast + throughput
- **Goal:** `ReaderWriterLockSlim` readers/writer; reads/sec + writes/sec; **RWLock ↔ plain lock** toggle
  showing the reader-throughput difference; clean restart on toggle.
- **Files:** `Assets/Scripts/Scenarios/SyncPrimitivesDeadlockScenario.cs` (extend).
- **Type:** Code · **Traceability:** REQ-006,007,008 (rw part); AC-004,005.
- **Rollback risk:** Low (same new file).

### TASK_03 — Manual QA (validation)
- **Goal:** Verify AC-001..007 (deadlock occurs + fix clears; readers overlap under RWLock vs serialize
  under plain lock; clean teardown even when deadlocked; no cross-thread errors).
- **Type:** validation · **Traceability:** AC-001..007.
