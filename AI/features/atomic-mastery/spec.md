# Feature: Atomic Mastery scenario

## Changelog
- **2026-07-28:** Initial spec.

## Problem
The lab visualizes races, freezes, parallelism, producer/consumer and ThreadPool starvation, but nothing
covers **atomic operations** as a mastery topic: lock-free coordination with `Interlocked`, a
`CompareExchange` (CAS) retry loop, complex inter-thread communication built purely on atomics, and the
hardware cost of **false sharing**. That matrix cell — *master atomic operations* — has no scenario yet.

## Goal
A new `IThreadingScenario` — "Atomic Mastery" — with three switchable **modes**, each isolating one facet
of atomics under real contending threads, with a live throughput / contention readout:

- **Mode A — Lock-free counter:** N threads hammer one counter for a fixed window. Compare
  `Interlocked.Increment`, a hand-rolled **CAS loop** (`Interlocked.CompareExchange`), and a `lock`
  baseline — showing all three stay correct while the lock-free paths win on throughput.
- **Mode B — Lock-free Treiber stack:** a `push`/`pop` stack whose head is swung with a
  `CompareExchange` **CAS retry loop**; producers and consumers coordinate purely through the atomic head
  pointer. Visualize CAS **retries** (contention) and a conservation invariant (nothing lost/duplicated).
- **Mode C — False sharing:** two "hot" counters incremented by separate threads. A layout toggle places
  them **packed** on the same cache line vs **padded** onto separate cache lines; the same run shows a
  throughput **cliff** when packed.

Matrix cell: **master atomic operations** (lock-free coordination, CAS loops, atomic inter-thread
communication, false sharing).

## Non-goals
- No `lock`-free *everything* crusade — the `lock` path in Mode A exists only as a comparison baseline.
- Not a general concurrent-collections library; the Treiber stack is a teaching artifact, not production code.
- No memory-model deep dive (fences/`volatile` semantics beyond what the demo needs); false sharing is
  shown empirically, and results are hardware/JIT dependent — the help text says so.
- No `async/await`; this is about raw threads + atomics.

## User scenarios
### Scenario A — Lock-free beats the lock, both stay correct
1. User selects "Atomic Mastery", Mode A. Sets thread count high, presses **Run**.
2. Each strategy (Interlocked / CAS loop / lock) runs the same fixed window; the final count equals the
   expected total for all three (correct), and ops/sec is markedly higher for the lock-free paths.

### Scenario B — CAS retries reveal contention
1. User switches to Mode B, raises thread count, presses **Run**.
2. Threads push and pop through the atomic head; the **CAS retry** counter climbs with contention, while
   the conservation check (pushed = popped + still-on-stack) holds — no node lost or double-popped.

### Scenario C — The false-sharing cliff
1. User switches to Mode C with **packed** layout, presses **Run**, notes total ops/sec.
2. User flips to **padded** layout, runs again → throughput jumps sharply (the cliff), demonstrating that
   two counters sharing a cache line silently serialize on the coherency protocol.

## Functional requirements

### REQ-001 — Scenario registered and selectable
`AtomicMasteryScenario : IThreadingScenario` under `Assets/Scripts/Scenarios/`, registered in
`ThreadingLabHost.BuildScenarios()`, titled "6 · Atomic Mastery", appears in the picker.

### REQ-002 — Mode selector
An in-scenario segmented control switches between Mode A (Lock-free counter), Mode B (Treiber stack),
Mode C (False sharing). Switching modes stops any in-flight run cleanly before showing the new mode.

### REQ-003 — Shared timed worker harness
A single internal helper runs a fixed-duration workload on N worker threads (`new Thread`), started on
**Run**, cancellable, and **joined** when the run ends, on mode switch, and on `Exit`. It records elapsed
time and total work so throughput (ops/sec) can be derived on the main thread.

### REQ-004 — Mode A: Interlocked.Increment path
A strategy where each worker calls `Interlocked.Increment` on a shared `long` counter in a tight loop for
the window. Final value equals the exact total number of increments (correctness).

### REQ-005 — Mode A: CAS-loop path
A strategy where each increment is a hand-rolled `Interlocked.CompareExchange` retry loop
(`read → compute → CAS → retry on mismatch`). Same correctness guarantee; illustrates the CAS primitive
underlying `Interlocked.Increment`.

### REQ-006 — Mode A: lock baseline
A `lock`-guarded increment strategy for comparison. Correct, but throughput is the baseline the lock-free
paths beat. All three strategies report ops/sec for the same thread count + window.

### REQ-007 — Mode B: Treiber stack via CAS
A lock-free stack whose `Push`/`Pop` swing the head with `Interlocked.CompareExchange` in a retry loop.
Workers push and pop for the window using only the atomic head — no locks.

### REQ-008 — Mode B: CAS retry visibility
Count CAS **retries** (failed `CompareExchange` attempts) with an `Interlocked` counter and display them —
retries rise with thread count, making contention visible.

### REQ-009 — Mode B: conservation invariant
Track pushed, popped, and nodes remaining on the stack; the display shows `pushed = popped + remaining`
holds after the run — no lost, leaked, or double-counted node (the ABA-safe-enough teaching claim; see
Non-goals / help text on ABA).

### REQ-010 — Mode C: packed vs padded layout
Two hot `long` counters laid out **packed** (same cache line) and **padded** (each on its own cache line,
via explicit struct layout / ≥64-byte separation). A toggle selects which layout the run uses.

### REQ-011 — Mode C: throughput cliff
Each layout runs the same fixed window with one thread per counter; total ops/sec is shown. Packed yields
markedly lower total throughput than padded — the false-sharing cliff — with help text noting the result
is hardware/JIT dependent.

### REQ-012 — Controls
Adjustable **thread count** and **run duration (ms)**; per-mode selectors (Mode A strategy, Mode C layout);
a **Run** button and a **Reset** (clear last results). No control mutates state consumed by a live run
unsafely (changes apply to the next run).

### REQ-013 — Lifecycle & cancellation
A `CancellationTokenSource` (or equivalent stop flag) bounds every run; workers observe it promptly. All
threads are **joined** before a run is considered finished and unconditionally on mode switch and `Exit()`
— no leaked or runaway workers.

### REQ-014 — No Unity API off the main thread
Workers touch only atomics / local state / the stop token. All counters, throughput, and invariants are
read and rendered on the main thread.

### REQ-015 — No per-frame churn
IMGUI styles cached (built once); `DrawGUI` reads a few counters + cached last-run results — no per-frame
thread work or heavy allocation.

## Failure scenarios
- **Exit / mode switch mid-run:** the token stops the workers and every thread is joined — no runaway loop
  keeps burning a core after the scenario is gone.
- **Run pressed twice / while running:** ignored or safely restarted (a new run only starts once the prior
  run's threads are joined) — no overlapping runs corrupting shared counters.
- **False sharing doesn't reproduce on some hardware:** treated as a known caveat, surfaced in help text,
  not a correctness failure.

## Analytics
- none.

## Data / persistence
- none.

## Platform constraints
- Editor / Standalone only. WebGL is out of scope (single-threaded; no real worker threads). See
  `profile.yaml → platforms`.
- False-sharing magnitude varies by CPU / Mono-IL2CPP layout — demonstrative, not a hard number.

## UX / UI
- Mode selector (A/B/C). Common controls: thread count, run duration, Run, Reset.
- Mode A: three strategy results (Interlocked / CAS loop / lock) — final count (correct?) + ops/sec each.
- Mode B: pushed / popped / remaining, conservation check, CAS retries, ops/sec.
- Mode C: layout toggle (packed/padded), total ops/sec, and a bar contrasting the two layouts' throughput.
- Help text per mode explaining what the reader should observe.

## Acceptance criteria

### AC-001 — Selectable
- **Given** Play mode
- **When** the user opens the picker
- **Then** "6 · Atomic Mastery" is listed and selecting it shows the mode selector + controls.

### AC-002 — Mode A correctness
- **Given** any thread count and window
- **When** a Mode A run completes for Interlocked, CAS-loop, and lock strategies
- **Then** each strategy's final counter equals the exact expected total (no lost updates).

### AC-003 — Mode A lock-free throughput
- **Given** a high thread count
- **When** the run completes
- **Then** the Interlocked and CAS-loop paths report higher ops/sec than the lock baseline.

### AC-004 — Mode B lock-free stack + retries
- **Given** Mode B with several threads
- **When** a run completes
- **Then** the CAS retry counter is > 0 under contention and the conservation invariant
  `pushed = popped + remaining` holds.

### AC-005 — Mode C false-sharing cliff
- **Given** Mode C
- **When** the user runs packed then padded with the same settings
- **Then** padded total ops/sec is noticeably higher than packed (help text notes hardware dependence).

### AC-006 — Clean teardown
- **Given** a run in flight (or just finished)
- **When** the user switches mode or leaves the scenario
- **Then** the token stops the workers, all threads are joined, and no thread keeps running (thread count
  in the top strip settles).

### AC-007 — No cross-thread violations
- **Given** workers running
- **When** they update atomics
- **Then** no "can only be called from the main thread" error and no unhandled exception in the Console.

## Open questions
- [ ] **Modes as a segmented control vs three always-visible panels.** Recommended: segmented control (one
      active mode) to keep only one run + one visualization live at a time. Resolve at plan time.
- [ ] **False-sharing layout technique:** `[StructLayout(LayoutKind.Explicit)]` with `FieldOffset` for
      packed (0 / 8) vs padded (0 / 128), or a padded struct array. Recommended: explicit FieldOffset with
      ≥128-byte padding (covers 64/128-byte lines and adjacent-line prefetch). Resolve at plan time.
- [ ] **Mode A: run all three strategies sequentially in one Run, or one selected strategy per Run.**
      Recommended: run all three sequentially so the reader sees the comparison in one shot. Resolve at plan time.
- [ ] **ABA in the Treiber stack.** A pure pointer-CAS stack is ABA-prone in general. Recommended: keep it
      simple (pool-free `new` nodes so freed nodes aren't recycled during a run) and call out ABA in the
      help text rather than adding tags/hazard pointers. Resolve at plan time.
