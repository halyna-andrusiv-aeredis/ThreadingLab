# Task 01 — Core bounded producer/consumer + lifecycle

## Goal
Create `ProducerConsumerScenario`, register it, and implement the bounded buffer: `ConcurrentQueue<int>`
+ two `SemaphoreSlim`, long-lived producer/consumer threads with fixed default counts, and a
`Start`/`Stop` lifecycle (cancel + join) wired to `Enter`/`Exit`. Basic metrics (depth, produced,
consumed). Threads must stop cleanly on Exit.

## Traceability
- **requirements:** REQ-001, REQ-002, REQ-003, REQ-004 (fixed counts), REQ-006, REQ-007 (basic), REQ-008, REQ-011
- **acceptance:** AC-001, AC-006, AC-007

## Files allowed to touch
- Assets/Scripts/Scenarios/ProducerConsumerScenario.cs (new)
- Assets/Scripts/Core/ThreadingLabHost.cs (add one registration line in BuildScenarios)

## Acceptance
- [ ] Scenario appears in the picker and is selectable.
- [ ] Shared buffer is a `ConcurrentQueue<int>`; bounded by `SemaphoreSlim _emptySlots` (capacity) and `_fullSlots` (0).
- [ ] Producer: `_emptySlots.Wait(ct)` → Enqueue → `_fullSlots.Release()`; Consumer: `_fullSlots.Wait(ct)` → TryDequeue → process → `_emptySlots.Release()`.
- [ ] Queue depth never exceeds capacity.
- [ ] `Enter()` starts the workers; `Exit()` cancels the token and **joins** every thread — no leaked/runaway threads (process-thread count returns to baseline).
- [ ] Counters via `Interlocked`; workers touch no `UnityEngine` API.
- [ ] Basic metrics shown: depth, produced, consumed. Cached IMGUI styles.
- [ ] Compiles clean (G2).
