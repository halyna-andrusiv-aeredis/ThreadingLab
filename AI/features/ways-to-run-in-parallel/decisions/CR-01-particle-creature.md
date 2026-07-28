# CR-01 — Switch payload from per-pixel field to authentic particle system

**Date:** 2026-07-24
**Trigger:** G4 manual QA — the per-pixel `sin/cos` field rendered a radially-symmetric "mandala",
not the organic creature. The reference (@yuruyurau, Processing) is a **particle system**, not a
field: ~10k points, each index mapped through trig to a screen position and plotted; the creature
is the accumulated point cloud.

## Change
Re-implement the workload as the authentic particle system, scaled up (point-count = weight), and
render by **accumulation** (points splat into a brightness buffer → mapped to red-orange).

This changes how the parallel methods work, which is itself pedagogically valuable:
- The field version was **embarrassingly parallel** (each pixel written by exactly one worker).
- A particle/scatter workload has **shared-write hazards** (two workers may splat the same pixel).
  We keep it race-free the idiomatic way: **each worker accumulates into its own buffer, then the
  main thread merges** them. New lesson: not every workload parallelizes for free — scatter needs
  per-thread state + a merge step (extra overhead = part of the "downside").

## Impact on spec
- **REQ-002** payload: per-pixel field → particle system (point cloud, additive accumulation).
- **REQ-005 / REQ-006** parallel methods now partition **points** (not rows); each partition
  accumulates into its **own** buffer; the main thread merges.
- **REQ-008** "row-disjoint writes" → "per-thread accumulation buffers + main-thread merge";
  texture upload stays main-thread-only.
- **REQ-010** weight control: resolution steps → **point-count steps** (250k / 1M / 2M / 4M).
  Canvas resolution is now fixed (512×512).
- ACs unchanged in intent (animates / Sequential slow / parallel faster / identical image / no
  cross-thread errors / no leak); "resolution" wording becomes "point count".

## Impact on threading model / risk
- No shared-write race (per-thread buffers). Merge + per-frame buffer clears are new overhead,
  borne on the main thread — an intended, visible cost.
- Buffers (`_accum`, `_pixels`, and P per-thread buffers) allocated **once** on Enter (fixed
  canvas), so no per-frame or per-weight-change texture/buffer churn.

## Tasks
TASK_01 and TASK_02 are re-scoped to the particle model and re-implemented (were `done` under the
field model; reset to re-run through G2 + G3 + G5 + G4). No new task ids.
