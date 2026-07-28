# Plan: Producer / Consumer scenario

Traces `spec.md` (REQ-001..011, AC-001..007). Stack per `AI/profile.yaml`: no DI, IMGUI, raw
`System.Threading`, QA target Editor/Standalone (not WebGL). Long-lived worker threads (unlike the
per-frame scenarios) — lifecycle + cancellation are the key discipline.

## 1. Architecture proposal

One new scenario class + one registration line.

- **`ProducerConsumerScenario : IThreadingScenario`** (new, `Assets/Scripts/Scenarios/`).
  - Shared buffer: `ConcurrentQueue<int> _queue`.
  - Bounded buffer: `SemaphoreSlim _emptySlots` (init capacity), `SemaphoreSlim _fullSlots` (init 0).
  - Workers: `List<Thread> _workers` (N producers + M consumers), a `CancellationTokenSource _cts`.
  - Counters (all `Interlocked` / `volatile`): `_produced`, `_consumed`, `_blockedProducers`,
    `_idleConsumers`; plus a 1-second rolling throughput sampler.
  - **Producer loop:** `while (!ct) { Interlocked.Increment(ref _blockedProducers); _emptySlots.Wait(ct);
    Interlocked.Decrement(ref _blockedProducers); _queue.Enqueue(x); _fullSlots.Release();
    Interlocked.Increment(ref _produced); Thread.Sleep(producerInterval); }` (wrap in try/catch
    `OperationCanceledException` to exit cleanly).
  - **Consumer loop:** `while (!ct) { Interlocked.Increment(ref _idleConsumers); _fullSlots.Wait(ct);
    Interlocked.Decrement(ref _idleConsumers); _queue.TryDequeue(out _); _emptySlots.Release();
    Interlocked.Increment(ref _consumed); Thread.Sleep(consumerWork); }`.
  - **Start(): ** build semaphores + queue + threads from current settings, start them.
  - **Restart(): ** `Stop()` then `Start()` — used when settings change.
  - **Stop(): ** `_cts.Cancel()`, `Join()` every worker (with a timeout guard), dispose semaphores/cts.
  - `Enter()` → `Start()`. `Exit()` → `Stop()`. `Tick()` updates the throughput sampler only.
  - `DrawGUI`: queue-depth bar (`_queue.Count` vs capacity), counters, backpressure/starvation
    indicators, and sliders/steppers for producers, consumers, consumer work, capacity (each triggers
    `Restart()` on change).

Resolved open questions: consumer work = short `Thread.Sleep` scaled by the slider; throughput = 1-second rolling items/sec.

## 2. Dependency / impact analysis

- Depends on `IThreadingScenario` (stable). No dependency on `MainThreadDispatcher` (workers touch no
  Unity API; the main thread only reads counters).
- Blast radius: one additive `Add(...)` line in the shell; the rest self-contained. The only shared
  mutable state is inside this scenario. Long-lived threads make **teardown correctness** the main risk.

## 3. Files to create / change

- **Create:** `Assets/Scripts/Scenarios/ProducerConsumerScenario.cs`
- **Change:** `Assets/Scripts/Core/ThreadingLabHost.cs` (one registration line)

## 4. Risks

- **Thread leak / hang on teardown.** A worker blocked in `Wait()` must be released by the token, not
  left waiting. Mitigation: pass the token to `Wait(ct)`; `Stop()` cancels then joins with a timeout;
  `Exit()` always calls `Stop()`. (AC-006.)
- **Reconfigure races.** Rapid setting changes could start a new set before the old is fully stopped.
  Mitigation: `Restart()` fully `Stop()`s (cancel+join) before `Start()`; guard against re-entrancy.
- **Semaphore/counter drift.** `_blockedProducers`/`_idleConsumers` must increment right before the
  wait and decrement right after, even on cancel (do the decrement in a `finally`). Mitigation: careful
  try/finally around each `Wait`.
- **Disposed semaphore use.** After `Stop()` disposes the semaphores, a lingering worker must not touch
  them. Mitigation: join before dispose; catch `ObjectDisposedException`/`OperationCanceledException`.

## 5. Step-by-step implementation plan (proposed task split)

### TASK_01 — Core bounded producer/consumer + lifecycle
- **Goal:** `ProducerConsumerScenario` registered; `ConcurrentQueue` + two `SemaphoreSlim`; fixed
  default producers/consumers; producer/consumer loops; `Start`/`Stop` with cancel+join; `Enter`/`Exit`
  wired; basic metrics (depth, produced, consumed). Threads stop cleanly on Exit.
- **Files:** `Assets/Scripts/Scenarios/ProducerConsumerScenario.cs` (new),
  `Assets/Scripts/Core/ThreadingLabHost.cs` (register).
- **Type:** Code
- **Expected result:** Selecting the scenario runs producers/consumers; the depth bar moves; leaving it
  stops all threads (no leak).
- **Validation/check:** G2 compile; Play-mode: threads start/stop, thread count returns to baseline on exit.
- **Traceability:** REQ-001, 002, 003, 004 (fixed counts), 006, 007 (basic), 008, 011.
- **Rollback risk:** Low (additive; one shell line).

### TASK_02 — Controls + backpressure/starvation indicators + throughput
- **Goal:** Sliders/steppers for producers, consumers, consumer work, capacity with clean `Restart()`;
  backpressure (blocked producers) + starvation (idle consumers) indicators; 1-second throughput.
- **Files:** `Assets/Scripts/Scenarios/ProducerConsumerScenario.cs` (extend).
- **Type:** Code
- **Expected result:** Slow consumers → queue pins at capacity + producers blocked; fast consumers →
  queue near-empty + consumers idle; live reconfigure restarts workers without leak/deadlock.
- **Validation/check:** G2 compile; Play-mode: exercise both regimes + reconfigure.
- **Traceability:** REQ-005, 009, 010; AC-002, 003, 004, 005.
- **Rollback risk:** Low (same new file).

### TASK_03 — Manual QA (validation)
- **Goal:** Verify AC-001..007 (backpressure, starvation, counter consistency, clean reconfigure, clean
  teardown, no cross-thread errors).
- **Files:** none (manual).
- **Type:** validation
- **Traceability:** AC-001..007.
- **Rollback risk:** n/a.
