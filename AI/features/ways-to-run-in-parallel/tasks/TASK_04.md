# Task 04 — Parallelize merge + colorize (CR-02)

## Goal
Move the per-thread-buffer merge and the accumulator→pixels colorize off the serial main-thread path:
combine them into one `Parallel.For` over disjoint pixel ranges, and upload with `SetPixelData`.

## Traceability
- **requirements:** REQ-008 (extends: parallel merge stays race-free), REQ-012 (per-frame overhead)
- **change:** CR-02

## Files allowed to touch
- Assets/Scripts/Scenarios/WaysToRunInParallelScenario.cs

## Acceptance
- [ ] Merge (per-thread buffers → `_accum`) and colorize (`_accum` → `_pixels`) run in one parallel
      pass over **disjoint** pixel ranges — each range writes only its own `_accum`/`_pixels` slice.
- [ ] Sequential path (no thread buffers) colorizes `_accum` directly; parallel paths merge first.
- [ ] Texture upload (`SetPixelData` + `Apply`) stays on the main thread, after the parallel pass joins.
- [ ] No shared-write race; image identical to before (white creature).
- [ ] No new per-frame allocations in the timed path beyond what TPL requires.
- [ ] Compiles clean (G2).
