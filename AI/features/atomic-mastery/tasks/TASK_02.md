# Task 02 — Mode A: lock-free counter (Interlocked / CAS loop / lock)

## Goal
Implement **Mode A — Lock-free counter**. One **Run** executes three strategies **sequentially** over the
same thread count + window against a shared `long _counter` (reset between strategies):
`Interlocked.Increment`; a hand-rolled **CAS loop**
(`do { o = Volatile.Read(ref _counter); } while (Interlocked.CompareExchange(ref _counter, o + 1, o) != o)`);
and a `lock (_gate) { _counter++; }` baseline. For each strategy record final count, expected total, and
elapsed ms → derive ops/sec. Display all three: final count vs expected (correct?) and ops/sec. Help text
explaining that lock-free stays correct while beating the lock, and that `Interlocked.Increment` is the
CAS loop done in one instruction.

## Traceability
- **requirements:** REQ-004, REQ-005, REQ-006
- **acceptance:** AC-002, AC-003

## Files allowed to touch
- Assets/Scripts/Scenarios/AtomicMasteryScenario.cs (extend)

## Acceptance
- [ ] One Run executes Interlocked, CAS-loop, and lock strategies over the same thread count + duration.
- [ ] Each strategy's final counter equals the exact expected total (correctness), shown per strategy.
- [ ] ops/sec reported per strategy; Interlocked and CAS-loop report higher ops/sec than the lock baseline at high thread count.
- [ ] CAS loop uses `Interlocked.CompareExchange` with a retry-on-mismatch loop.
- [ ] Workers touch only the shared counter/atomics; results read on the main thread. Help text present.
- [ ] Compiles clean (G2).
