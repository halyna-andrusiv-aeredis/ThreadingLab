# Manual QA — producer-consumer (Gate G4)

Run in the Unity Editor (Play mode). Primary QA target: Editor / Standalone. WebGL is not a target.

## Setup
1. Open `C:\Projects\ThreadingLab` in Unity 6000.0.66f2 and press **Play**.
2. In the left picker, select **"4 · Producer / Consumer"**.

## Checks (mapped to acceptance criteria)

- [ ] **AC-001 — Selectable.** The scenario appears; selecting it shows the queue-depth bar, counters, and steppers (Producers / Consumers / Capacity / Producer ms / Consumer ms).
- [ ] **AC-002 — Backpressure (producers faster).** With the defaults (2 producers @40ms vs 2 consumers @90ms), the depth bar fills to **capacity** and stays pinned there; **Backpressure** shows N producers blocked (> 0); the depth never grows past capacity. Raise **Consumer ms** to exaggerate.
- [ ] **AC-003 — Starvation (consumers faster).** Lower **Consumer ms** to ~0 and/or raise **Consumers** (or lower producers) → the depth drops to ~0 and **Starvation** shows consumers idle (> 0).
- [ ] **AC-004 — Counters consistent.** Produced and Consumed both climb; Consumed ≤ Produced; (Produced − Consumed) ≈ current depth + items being processed. Throughput (/s) is shown.
- [ ] **AC-005 — Clean live reconfigure.** Change **Producers / Consumers / Capacity** a few times → each change restarts the workers (a brief stall is expected/by design) with **no Console error**; the top-strip **process threads** count settles to the new set (no accumulation/leak). Changing **Producer ms / Consumer ms** applies live without a restart.
- [ ] **AC-006 — Clean teardown.** Switch to another scenario and back a few times → all producer/consumer threads stop; the process-thread count returns to baseline (no runaway).
- [ ] **AC-007 — No cross-thread errors.** Console shows no "can only be called from the main thread" error and no unhandled exception (a `Debug.LogWarning` only if a worker ever fails to stop in time — should not normally appear).

## Result
- Tester: <name> — <date>
- Outcome: PASS / FAIL (list any failing AC)
