# Task 03 — Mode B: Treiber stack (CAS retry loop + conservation)

## Goal
Implement **Mode B — Treiber stack**. An internal lock-free `TreiberStack` with `Node { long value; Node next }`
whose head is swung by `Interlocked.CompareExchange` in a **retry loop** for both `Push` and `Pop`. Workers
push then pop for the window using only the atomic head (no locks). Nodes are allocated fresh with `new`
(no free-list recycling during a run) to sidestep ABA. `Interlocked` counters: `_pushed`, `_popped`,
`_retries` (increment on each failed CAS). After all threads join, count `remaining` nodes on the stack and
display the conservation check `pushed = popped + remaining`, plus CAS retries and ops/sec. Help text notes
what CAS retries mean under contention and that a pointer-only Treiber stack is ABA-prone in general (why
fresh nodes here).

## Traceability
- **requirements:** REQ-007, REQ-008, REQ-009
- **acceptance:** AC-004

## Files allowed to touch
- Assets/Scripts/Scenarios/AtomicMasteryScenario.cs (extend)

## Acceptance
- [ ] `TreiberStack.Push`/`Pop` swing the head via `Interlocked.CompareExchange` in a retry loop; no locks.
- [ ] Workers push and pop for the window; nodes allocated with `new` (no recycling during a run).
- [ ] `Interlocked` counters for pushed, popped, retries; retries > 0 under contention (several threads).
- [ ] After join, `remaining` counted; display shows `pushed = popped + remaining` holds (no lost/duplicated node).
- [ ] Workers touch only atomics/local nodes; counters read on the main thread. Help text incl. ABA note.
- [ ] Compiles clean (G2).
