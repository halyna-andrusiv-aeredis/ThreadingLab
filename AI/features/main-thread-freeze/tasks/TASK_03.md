# Task 03 — Manual QA (validation)

## Goal
Verify the scenario in Play mode against the spec's acceptance criteria: the freeze is visible
on the main-thread run, the background run stays smooth, re-entrancy is blocked, and switching
scenarios mid-run leaves no leaked worker.

## Traceability
- **acceptance:** AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007

## Files allowed to touch
- (none — manual QA)

## Acceptance
- [ ] AC-001 — selectable in the picker.
- [ ] AC-002 — idle indicator animates; FPS normal.
- [ ] AC-003 — main-thread run freezes the indicator; FPS ~0; result shown after.
- [ ] AC-004 — background run keeps animating; FPS high; result shown on completion.
- [ ] AC-005 — run buttons disabled during a run.
- [ ] AC-006 — no cross-thread errors/exceptions in the Console.
- [ ] AC-007 — switching scenario mid-run leaves no stuck worker; UI consistent.
