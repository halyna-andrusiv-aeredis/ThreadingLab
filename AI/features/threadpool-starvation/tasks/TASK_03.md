# Task 03 — Manual QA (validation)

## Goal
Verify the scenario in Play mode: starvation with default min, mitigation via SetMinThreads, counter
consistency, clean teardown (cancel + restore min threads), and no cross-thread Console errors.

## Traceability
- **acceptance:** AC-001, AC-002, AC-003, AC-004, AC-005, AC-006

## Files allowed to touch
- (none — manual QA)

## Acceptance
- [ ] AC-001 — selectable in the picker.
- [ ] AC-002 — big block ms + burst above core count, default min: Started plateaus near core count, backlog stays high, Completed rises slowly, ThreadPool thread count creeps up over seconds.
- [ ] AC-003 — raise min pool threads high, submit the same burst: nearly all start at once, backlog ~0, completes quickly.
- [ ] AC-004 — completed ≤ started ≤ submitted; in-flight = started − completed; backlog = submitted − started.
- [ ] AC-005 — leaving the scenario cancels in-flight waits (no runaway; thread count settles) and restores the original min threads.
- [ ] AC-006 — no cross-thread errors/exceptions in the Console.
