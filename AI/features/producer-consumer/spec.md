# Feature: Producer / Consumer scenario

## Changelog
- **2026-07-24:** Initial spec.

## Problem
The learner needs to *see* how threads hand work to each other safely, and what happens when
production and consumption rates differ. A live bounded producer/consumer makes the concurrent
collection, the semaphores, and backpressure/starvation visible.

## Goal
A new `IThreadingScenario` — "Producer / Consumer" — with N long-lived **producer** threads and M
**consumer** threads exchanging items through a **`ConcurrentQueue<int>`** bounded by two
**`SemaphoreSlim`** (the classic bounded-buffer). The user adjusts producer/consumer counts and
speeds and watches the **queue depth**, throughput, **backpressure** (producers blocked when full)
and **starvation** (consumers idle when empty) in real time.

Matrix cells: **thread synchronization with locks/concurrent collections**, **semaphores** (a slim
counterpart), and safe multi-thread coordination + lifecycle (cancellation, join on teardown).

## Non-goals
- No `async/await` — this uses real threads + blocking `SemaphoreSlim.Wait` on purpose (to show the primitive).
- No persistence / distributed queue.
- No lock-based `Queue<T>` vs `ConcurrentQueue` benchmark (mention in the help text only; keep focused).
- No `BlockingCollection<T>` wrapper — build the bounded buffer from the primitives so they are visible.

## User scenarios
### Scenario A — Backpressure vs starvation
1. User selects "Producer / Consumer". Producers and consumers run; the queue-depth bar moves.
2. User makes consumers slow (or fewer) → the queue fills to capacity, **producers block** (backpressure), depth pinned at max — no unbounded growth.
3. User makes consumers fast (or more) → the queue stays near-empty, **consumers idle** (starved).

## Functional requirements

### REQ-001 — Scenario registered and selectable
`ProducerConsumerScenario : IThreadingScenario` under `Assets/Scripts/Scenarios/`, registered in
`ThreadingLabHost.BuildScenarios()`, appears in the picker.

### REQ-002 — Concurrent collection
The shared buffer is a **`System.Collections.Concurrent.ConcurrentQueue<int>`** (thread-safe handoff).

### REQ-003 — Bounded buffer via two semaphores
Bounded with `SemaphoreSlim _emptySlots` (initial = capacity) and `SemaphoreSlim _fullSlots`
(initial = 0). Producer: `_emptySlots.Wait(ct)` → `Enqueue` → `_fullSlots.Release()`. Consumer:
`_fullSlots.Wait(ct)` → `TryDequeue` → process → `_emptySlots.Release()`. Depth never exceeds capacity.

### REQ-004 — Adjustable producers and consumers
N producer threads and M consumer threads (each a long-lived loop). Counts are user-adjustable.

### REQ-005 — Adjustable rates
Consumer simulates work per item (adjustable cost, e.g. a spin/sleep); producer has an adjustable
production interval. So the user can drive producer-faster vs consumer-faster regimes.

### REQ-006 — Lifecycle & cancellation
A `CancellationTokenSource` stops all workers. `Exit()` cancels and **joins** every producer/consumer
thread — no leaked or runaway threads after leaving the scenario.

### REQ-007 — Live metrics
Show: current queue depth vs capacity, total produced, total consumed, producers currently blocked
(backpressure), consumers currently idle (starved), and throughput (items/sec).

### REQ-008 — No Unity API off the main thread
Workers only touch the queue, the semaphores, and `Interlocked` counters. All counters read on the
main thread for display; no `UnityEngine` calls from worker threads.

### REQ-009 — Invariants visible
Queue depth is always ≤ capacity (semaphore guarantees it); when producers outpace consumers,
backpressure is visibly engaged (blocked producers) rather than the queue growing without bound.

### REQ-010 — Clean live reconfigure
Changing producer/consumer count, rates, or capacity **restarts** the worker set cleanly (cancel +
join the old set, start a new one) — no thread leak, no deadlock, counters reset or continue sensibly.

### REQ-011 — No per-frame churn
IMGUI styles cached; per-frame display reads simple counters (no allocation storms in `DrawGUI`).

## Failure scenarios
- Cancellation while a producer is blocked on `_emptySlots.Wait(ct)` (queue full) or a consumer on
  `_fullSlots.Wait(ct)` (queue empty): the token releases the wait (OperationCanceledException) and the
  thread exits its loop — no hang on `Exit()`/reconfigure.
- Reconfigure spam (rapidly changing counts): each restart fully tears down the previous set first.

## Analytics
- none.

## Data / persistence
- none.

## Platform constraints
- Editor / Standalone only. WebGL is out of scope (single-threaded). See `AI/profile.yaml → platforms`.

## UX / UI
- Queue-depth bar (0..capacity) + numbers; produced/consumed counters; backpressure & starvation
  indicators; controls for producers, consumers, consumer work, capacity.

## Acceptance criteria

### AC-001 — Selectable
- **Given** Play mode
- **When** the user opens the picker
- **Then** "Producer / Consumer" is listed and selecting it shows the running queue + controls.

### AC-002 — Backpressure (producer faster)
- **Given** slow/few consumers relative to producers
- **When** the queue reaches capacity
- **Then** the depth bar pins at max, producers show as **blocked** (backpressure), and the queue never grows past capacity.

### AC-003 — Starvation (consumer faster)
- **Given** fast/many consumers relative to producers
- **When** the queue drains
- **Then** the depth stays near zero and consumers show as **idle/starved**.

### AC-004 — Counters consistent
- **Given** the scenario runs
- **When** watching produced/consumed totals
- **Then** both climb, consumed ≤ produced, and (produced − consumed) ≈ current depth + items in processing.

### AC-005 — Clean live reconfigure
- **Given** the scenario is running
- **When** the user changes producers/consumers/capacity/work
- **Then** the worker set restarts with no Console error and the process-thread count settles to the new set (no leak/accumulation).

### AC-006 — Clean teardown
- **Given** the scenario is active
- **When** the user switches to another scenario
- **Then** all producer/consumer threads stop (process-thread count returns to baseline); nothing keeps running.

### AC-007 — No cross-thread violations
- **Given** workers are running
- **When** they produce/consume
- **Then** no "can only be called from the main thread" error and no unhandled exception in the Console.

## Open questions
- [ ] Consumer work model: `Thread.Sleep(ms)` (cheap, frees the core) vs a CPU spin (shows real load).
      Recommended: a short `Thread.Sleep` scaled by the work slider (clear + doesn't burn cores). Resolve at plan time.
- [ ] Throughput window: instantaneous vs 1-second rolling. Recommended: 1-second rolling items/sec. Resolve at plan time.
