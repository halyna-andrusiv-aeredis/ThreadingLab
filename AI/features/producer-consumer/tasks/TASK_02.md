# Task 02 — Controls + backpressure/starvation indicators + throughput

## Goal
Add user controls (producers, consumers, consumer work, capacity) that cleanly `Restart()` the worker
set, plus backpressure (blocked producers) and starvation (idle consumers) indicators and a 1-second
rolling throughput.

## Traceability
- **requirements:** REQ-005, REQ-009, REQ-010
- **acceptance:** AC-002, AC-003, AC-004, AC-005

## Files allowed to touch
- Assets/Scripts/Scenarios/ProducerConsumerScenario.cs (extend)

## Acceptance
- [ ] Controls for producer count, consumer count, consumer work, and capacity; changing any triggers a clean `Restart()` (Stop = cancel + join, then Start).
- [ ] Blocked-producers count (backpressure) and idle-consumers count (starvation) tracked via `Interlocked` (increment before `Wait`, decrement after, even on cancel) and shown.
- [ ] Queue-depth bar drawn (0..capacity).
- [ ] Slow/few consumers → depth pins at capacity, producers shown blocked, no unbounded growth.
- [ ] Fast/many consumers → depth near zero, consumers shown idle.
- [ ] 1-second rolling throughput (items/sec) shown.
- [ ] Live reconfigure leaves no thread leak or deadlock (thread count settles to the new set).
- [ ] Compiles clean (G2).
