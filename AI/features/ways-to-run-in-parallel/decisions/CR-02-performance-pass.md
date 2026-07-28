# CR-02 — Performance pass

**Date:** 2026-07-24
**Trigger:** G4 manual QA — absolute FPS was low and the parallel speedup underwhelming. Root cause:
the per-frame workload is transcendental-heavy (FPU-bound), plus serial per-frame overhead on the
main thread (merge of per-thread buffers + texture colorize/upload). "8 cores" is ~4 physical + HT,
which barely helps FPU work.

## Already applied during the QA loop (documented here for the record)
These were applied ad-hoc while iterating with the tester and are now folded into the feature record:
- **Per-point cache of the time-independent formula parts** — `k`, `d`, `atan2`, `sin(y/19)` depend
  only on the point index, not on `t`. Cached (rebuilt when point count changes), so the per-frame
  hot path does **3 trig ops/point instead of 8** (~2.6× less per-frame math). Cache is read-only
  during the parallel section → no new race.
- `System.MathF` instead of Unity `Mathf` for the point math (helps in an IL2CPP build; Mono/Editor
  is unchanged, but harmless).
- Canvas **512→400** (less clear/merge/colorize/upload work; better cache locality for the scatter).
- Visual: **white** creature on near-black (matches the source); anim speed ×8→×12.
- Metric fix: responsive **Frame ms** shown next to **Compute ms** (the old FPS was over-smoothed and
  misleading).
- Default point count **1M→250k** for a smoother first impression.

## This step (new)
- **Parallelize the merge + colorize.** The per-thread buffer merge and the accumulator→pixels
  colorize were serial on the main thread. Combine them into one `Parallel.For` over disjoint pixel
  ranges (each range writes its own `_accum`/`_pixels` slice → no race; the texture upload stays
  main-thread only). Removes the serial merge/colorize from the frame's critical path.
- **`SetPixelData` instead of `SetPixels32`** for a cheaper upload (raw copy, no per-pixel marshaling).

## Impact on threading model / risk
- New parallel section: the merge/colorize `Parallel.For` writes **disjoint** pixel ranges of
  `_accum` and `_pixels`, and reads all `_threadAccum[*]` (shared read) — race-free, same discipline
  as the plot phase. `SetPixelData`/`Apply` remain main-thread only.
- Expected gain is **modest** — the point compute still dominates; profiling shows the cache (above)
  was the big win. This step trims the serial tail. (Honest note: not every optimization pays off big;
  measure first.)

## Tasks
Adds **TASK_04** (parallelize merge + colorize + SetPixelData). Re-run G2 → G3 → G5 → G4.
