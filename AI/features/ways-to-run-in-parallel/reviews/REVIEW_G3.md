# Gate G3 — Independent feature review: ways-to-run-in-parallel

Two cold independent `reviewer` passes.

## Pass 1 (pre-CR, per-pixel field) — Approved
- Verified: no texture leak, row-disjoint writes (no race), no Unity API off-thread, ThreadPool
  `CountdownEvent` safe.
- Should-fix (applied): one-frame uninitialized-texture flash → primed the texture on `Alloc`.
- Nice-to-have (deferred): per-frame parallel-method allocations (teaching-readability trade-off).

## Pass 2 (after CR-01, authentic particle system) — Approved
New threading model: scatter workload, per-thread accumulation buffers + main-thread merge.
- **Verified no shared-write race:** each `_threadAccum[idx]` has exactly one writer; merge/Sequential
  run only after the join, main-thread only; `MapPoint`/`PlotRange` are pure (`Mathf` + `int[]`).
- **Merge determinism (REQ-007/008):** `_accum` and per-thread buffers cleared each frame; partitions
  are disjoint and cover `[0,n)`; integer addition is commutative → image bit-identical across methods.
- **ThreadPool:** `CountdownEvent(p)` with `p` items, `Signal()` in `finally` (no deadlock on worker
  exception); empty partitions still signal.
- **Lifetime:** buffers allocated once in `Enter`; texture destroyed + arrays nulled in `Exit`; changing
  point count only changes sampling density (buffers fixed W×H, fully bounds-checked, no OOB); no leak
  on re-entry (host calls `Exit` before `Enter`).
- **No Must-fix.**

### Findings applied (Pass 2)
- **Should-fix (partially applied):** per-frame `CountdownEvent`+closure allocations inside the timed
  `Accumulate` add noise to the compute-ms metric. **Applied:** cache the `CountdownEvent` and `Reset()`
  it per frame (removed the per-frame alloc+Dispose in the timed path). **Deferred:** hoisting the
  per-worker closures — TPL/ThreadPool allocate internally regardless, and the readable form suits a
  teaching demo (reviewer sanctioned deferral).
- **Nice-to-have (applied):** guard `ThreadPool.QueueUserWorkItem` return value — `Signal()` if the queue
  refuses, so `Wait()` can never hang.
- **Nice-to-have (deferred):** `catch` to surface a worker exception on the main thread — `PlotRange`
  cannot throw in practice (bounds-checked, pure math); documentation-level only.

## Pass 3 (after CR-02, parallel merge/colorize + per-point cache) — Approved
- **Per-point cache** (`RebuildCache` `Parallel.For`): writes disjoint indices, main-thread before the
  plot, read-only during it → no race. Reconstructed `PlotRange` against the full `MapPoint` — `q`, `c`,
  `px`, `py`, and the time args (`t/4`, `t/8`) match exactly; 3 trig/point. Correct & faithful.
- **`Present`** (parallel merge+colorize): `per=ceil(total/p)` ranges disjoint and cover `[0,W*H)`; each
  chunk writes only its own `_accum`/`_pixels`, reads `_threadAccum[*]` (shared read) → no race. Both
  paths correct (Sequential reads `_accum`; parallel sums thread buffers). `SetPixelData`+`Apply` main
  thread after join; no Unity API in workers. Correct.
- **No Must-fix.**

### Findings (Pass 3)
- **Should-fix (deferred):** per-frame closure allocation in `Present`/`Accumulate` `Parallel.For`.
  Reviewer: mild, partly inherent to TPL, task permits "what TPL requires" → deferred (readability of the
  teaching demo over micro-opt; consistent with the project's deliberate raw-TPL use).
- **Nice-to-have (applied):** comment noting `Present` colorizes in parallel even in Sequential mode and
  is not part of the Compute metric.
- **Nice-to-have (ignored):** boundary false-sharing — negligible.

## Resolution
All three G3 passes Approved. Re-run G2 (done, PASS) → G5 → G4.
