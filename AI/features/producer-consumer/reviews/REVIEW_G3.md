# Gate G3 — Independent feature review: producer-consumer

Reviewer: cold independent `reviewer` subagent. Verdict: **Approved** (no Must-fix).

## Verified
- **Depth ≤ capacity (REQ-009):** a producer holds an `_emptySlots` permit from `Wait` until `Enqueue`;
  permits conserved at `capacity` → `queue.Count ≤ capacity` always.
- **No `SemaphoreFullException`:** every `Release` is preceded by the matching `Wait`, so neither
  semaphore's count can exceed `capacity`.
- **Cancellation balance:** a producer cancelled after `_emptySlots.Wait` succeeds still completes the
  transfer; semaphores are disposed (never reused) after join, so cross-generation balance is moot.
- **Blocked/idle accounting:** `Increment` before `Wait`, `Decrement` in `finally` → balanced even when
  `Wait` throws on cancel.
- **No Unity API off-thread; no deadlock in Stop:** `Join(2000)` cannot deadlock (workers hold no lock
  the main thread needs; blocked `Wait`s release via the token; `Sleep ≤ 500 ms`). `Stop()` idempotent.
- Self-contained; host change is one additive registration line; no static/shared state.

## Findings applied
- **Should-fix (applied):** workers referenced the queue/semaphores through **fields**, which `Stop()`
  nulls and `Start()` rebinds — a thread outliving `Join(2000)` could touch the next generation's
  objects (corrupt accounting) or throw an uncaught NRE. **Fix:** pass the queue + both semaphores into
  `ProducerLoop`/`ConsumerLoop` as parameters (bind each worker to its own generation, like `ct`), and
  honor `Join`'s return value (`Debug.LogWarning` if a worker fails to stop). Removes the
  cross-generation corruption and the uncaught-NRE window.

## Nice-to-have
- Restart-hitch (Stop joins on the main thread inside OnGUI): by design (REQ-010), IMGUI-safe; added a
  code comment acknowledging the deliberate stall.
- DrawGUI string interpolation / non-`volatile` rate fields / `_nextItem` overflow — cosmetic; left as is.

## Resolution
Approved; Should-fix applied (lifecycle hygiene — on-theme for this project). Re-run G2 → G5 → G4.
