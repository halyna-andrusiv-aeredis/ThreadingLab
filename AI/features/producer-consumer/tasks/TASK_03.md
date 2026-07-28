# Task 03 — Manual QA (validation)

## Goal
Verify the scenario in Play mode: backpressure and starvation regimes, counter consistency, clean live
reconfigure, clean teardown (no thread leak), and no cross-thread Console errors.

## Traceability
- **acceptance:** AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007

## Files allowed to touch
- (none — manual QA)

## Acceptance
- [ ] AC-001 — selectable in the picker.
- [ ] AC-002 — slow/few consumers: queue pins at capacity, producers shown blocked (backpressure), no unbounded growth.
- [ ] AC-003 — fast/many consumers: queue near-empty, consumers shown idle (starvation).
- [ ] AC-004 — produced/consumed climb; consumed ≤ produced; (produced − consumed) ≈ depth + in-processing.
- [ ] AC-005 — changing producers/consumers/capacity/work restarts workers with no error; thread count settles (no leak).
- [ ] AC-006 — leaving the scenario stops all workers; process-thread count returns to baseline.
- [ ] AC-007 — no cross-thread errors/exceptions in the Console.
