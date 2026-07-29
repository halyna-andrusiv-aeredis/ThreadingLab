# Plan: Atomic Mastery scenario

Traces `spec.md` (REQ-001..015, AC-001..007). Stack per `AI/profile.yaml`: no DI, IMGUI, raw
`System.Threading`, QA target Editor/Standalone (not WebGL). Follows the established one-scenario-per-class
pattern (see `ThreadPoolStarvationScenario`) and the shared shell contract `IThreadingScenario` +
`ThreadingLabHost.BuildScenarios()`.

## Investigation report

1. **Spec selected:** `AI/features/atomic-mastery/spec.md` (this feature).
2. **Existing similar modules:** `ThreadPoolStarvationScenario` and `ProducerConsumerScenario` — same
   shape (settings + Interlocked counters + CTS-bounded workers + cached IMGUI styles + Stepper control).
   `ProducerConsumerScenario` already spawns/joins `new Thread` workers; reuse that discipline.
3. **Reusable patterns:** the `Stepper(label,value,min,max,step)` control, `EnsureStyles()` cached-style
   pattern, `_big/_mid/_tip` styles, CTS-on-Enter / cancel-join-on-Exit lifecycle, `Interlocked` counter
   holders read on the main thread. The registration is one additive line in `BuildScenarios()`.
4. **Assumptions:** three modes live in one scenario class (matrix cell is one competency); a small
   internal timed-run harness is shared across modes; false sharing is demonstrative (hardware dependent).
5. **Open questions — resolved:**
   - **Modes:** segmented control (one active mode; one live run + one visualization at a time).
   - **False-sharing layout:** `[StructLayout(LayoutKind.Explicit)]` — packed = FieldOffset 0 & 8,
     padded = FieldOffset 0 & 128 (≥ two cache lines apart, covers 64/128-byte lines + adjacent prefetch).
   - **Mode A:** one **Run** executes all three strategies **sequentially** (Interlocked → CAS loop →
     lock), so the comparison appears in one shot with identical thread count + window.
   - **ABA:** keep the Treiber stack simple — allocate fresh nodes with `new` (no free-list recycling
     during a run, so a popped node is not re-pushed with a stale address); note ABA in help text.

## 1. Architecture proposal

One new scenario class + one registration line. Internal structure:

- **`AtomicMasteryScenario : IThreadingScenario`** (new, `Assets/Scripts/Scenarios/`).
  - **Mode enum** `{ LockFreeCounter, TreiberStack, FalseSharing }` + selector row in `DrawGUI`.
  - **Common settings:** `_threads` (worker count), `_runMs` (window). Cached styles `_big/_mid/_tip`.
  - **Run harness** — a small private helper that, given a per-thread `Action<CancellationToken>` (or a
    strategy delegate) and a duration, spawns `_threads` `new Thread`, starts a `Stopwatch`, lets them run
    until the token trips (a timer / `CancellationTokenSource.CancelAfter(_runMs)` plus a stop flag),
    **joins all threads**, and returns elapsed ms. One run at a time (guarded by an `_isRunning` flag);
    a new Run is refused/queued until the prior threads are joined.
  - **Lifecycle:** `Enter()` inits settings/CTS references; `Exit()` cancels the active run and joins any
    live threads (unconditional). Mode switch calls the same stop-and-join before changing mode.
  - **Mode A state:** `long _counter`; three strategies producing `(finalCount, expected, elapsedMs)` →
    ops/sec. `Interlocked.Increment`, a CAS loop
    (`do { o = Volatile.Read(ref _counter); } while (Interlocked.CompareExchange(ref _counter, o+1, o) != o)`),
    and a `lock(_gate){ _counter++; }`.
  - **Mode B state:** an internal lock-free `TreiberStack` (`Node{ value; next }`, head swung by
    `Interlocked.CompareExchange`), plus `Interlocked` counters `_pushed`, `_popped`, `_retries`; workers
    push then pop in a loop for the window. After join, `remaining` = drain/count nodes; assert
    `pushed == popped + remaining` for the display.
  - **Mode C state:** two layout structs via `[StructLayout(LayoutKind.Explicit)]` — `Packed{ [0] a; [8] b }`
    and `Padded{ [0] a; [128] b }`; exactly two worker threads, each hammering one field; total ops/sec
    per layout; a two-bar contrast (packed vs padded, last results cached).
  - `Tick()`: nothing heavy (results read in `DrawGUI`).

Worker threads touch only atomics / locals / the stop token; every counter and result is read on the main
thread for display (REQ-014). Styles built once (REQ-015).

## 2. Dependency / impact analysis

- Depends on `IThreadingScenario` (stable) and the `Stepper`/style patterns (copied per existing
  convention — the scenarios do not currently share a base class; keep it that way to avoid a refactor).
- No `MainThreadDispatcher` needed (no worker→Unity callbacks; main thread polls results).
- Blast radius: one additive `_scenarios.Add(new AtomicMasteryScenario())` line in `ThreadingLabHost`.
  No changes to `ProjectSettings/`, `Packages/`, `.meta`, scenes, or other scenarios.

## 3. Files to create / change

- **Create:** `Assets/Scripts/Scenarios/AtomicMasteryScenario.cs`
- **Change:** `Assets/Scripts/Core/ThreadingLabHost.cs` (one registration line)

## 4. Risks

- **Leaked / runaway workers.** Tight lock-free loops burn a core fast; if not joined on Exit/mode-switch
  they keep running. Mitigation: single `_isRunning` guard, token trips the loop, **join every thread**
  before finishing a run and unconditionally on `Exit`/mode switch (REQ-013, AC-006).
- **False sharing may not reproduce** on every CPU / under IL2CPP field layout. Mitigation: ≥128-byte
  padding, one thread pinned per counter, headline the *packed-vs-padded delta*; help text states it is
  hardware/JIT dependent (REQ-011, AC-005).
- **ABA in the Treiber stack.** Pointer-only CAS is ABA-prone. Mitigation: allocate fresh `new` nodes (no
  recycling during a run) so an address isn't reused mid-run; document ABA in help text (REQ-009 scope).
- **Cross-run counter staleness.** Reset must only clear results when no run is live. Mitigation: Reset is
  a no-op while `_isRunning`; cache last-run results as immutable snapshots.
- **`StructLayout.Explicit` pitfalls** (overlap / GC-ref fields). Mitigation: fields are blittable `long`s
  only, offsets non-overlapping; no reference-type fields under explicit layout.
- **Blocking join on the main thread.** Joining at end-of-run/`Exit` briefly blocks; runs are short
  (`_runMs`, default ~500 ms) and Exit joins are near-instant once the token trips. Acceptable for a lab.

## 5. Step-by-step implementation plan (proposed task split)

### TASK_01 — Scenario skeleton, registration, run harness, lifecycle
- **Goal:** `AtomicMasteryScenario` registered ("6 · Atomic Mastery"); mode enum + selector; common
  controls (thread count, run duration, Run, Reset); the shared timed **run harness** (spawn N threads,
  bound by token/duration, **join all**), `_isRunning` guard; `Enter`/`Exit` (cancel + join). Empty mode
  bodies wired but not yet computing results.
- **Files:** `Assets/Scripts/Scenarios/AtomicMasteryScenario.cs` (new),
  `Assets/Scripts/Core/ThreadingLabHost.cs` (register).
- **Type:** Code
- **Expected result:** Scenario selectable; switching modes and leaving the scenario joins all workers
  (top-strip thread count settles); no leaked threads.
- **Validation/check:** G2 compile; Play-mode: select, switch modes, leave — thread count returns to base.
- **Traceability:** REQ-001, 002, 003, 012, 013, 014, 015; AC-001, 006, 007.
- **Rollback risk:** Low (additive; one shell line).

### TASK_02 — Mode A: lock-free counter (Interlocked / CAS loop / lock)
- **Goal:** Mode A runs the three strategies sequentially over the same thread count + window; shows each
  final count vs expected (correctness) and ops/sec; help text.
- **Files:** `Assets/Scripts/Scenarios/AtomicMasteryScenario.cs` (extend).
- **Type:** Code
- **Expected result:** All three correct; Interlocked & CAS-loop ops/sec > lock baseline.
- **Validation/check:** G2 compile; Play-mode: run at high thread count, verify correctness + throughput gap.
- **Traceability:** REQ-004, 005, 006; AC-002, 003.
- **Rollback risk:** Low (same new file).

### TASK_03 — Mode B: Treiber stack (CAS retry loop + conservation)
- **Goal:** Lock-free `TreiberStack` (CAS `Push`/`Pop`); workers push/pop for the window; `Interlocked`
  counters for pushed/popped/retries; post-run `remaining` count; conservation display; help text (incl.
  ABA note).
- **Files:** `Assets/Scripts/Scenarios/AtomicMasteryScenario.cs` (extend).
- **Type:** Code
- **Expected result:** retries > 0 under contention; `pushed = popped + remaining` holds.
- **Validation/check:** G2 compile; Play-mode: raise threads, verify retries climb and invariant holds.
- **Traceability:** REQ-007, 008, 009; AC-004.
- **Rollback risk:** Low (same new file).

### TASK_04 — Mode C: false sharing (packed vs padded cliff)
- **Goal:** `[StructLayout(LayoutKind.Explicit)]` packed (0/8) vs padded (0/128); two threads, one per
  counter; total ops/sec per layout; two-bar contrast; help text (hardware-dependent caveat).
- **Files:** `Assets/Scripts/Scenarios/AtomicMasteryScenario.cs` (extend).
- **Type:** Code
- **Expected result:** padded total ops/sec noticeably higher than packed.
- **Validation/check:** G2 compile; Play-mode: run packed then padded, observe the cliff.
- **Traceability:** REQ-010, 011; AC-005.
- **Rollback risk:** Low (same new file).

### TASK_05 — Manual QA (validation)
- **Goal:** Verify AC-001..007 (selectable, Mode A correctness + throughput, Mode B retries + conservation,
  Mode C cliff, clean teardown/join, no cross-thread errors).
- **Files:** none (manual).
- **Type:** validation
- **Traceability:** AC-001..007.
- **Rollback risk:** n/a.
