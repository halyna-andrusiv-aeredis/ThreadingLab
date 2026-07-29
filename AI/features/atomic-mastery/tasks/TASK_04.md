# Task 04 — Mode C: false sharing (packed vs padded cliff)

## Goal
Implement **Mode C — False sharing**. Two hot `long` counters laid out two ways via
`[StructLayout(LayoutKind.Explicit)]`: **packed** (`FieldOffset(0)` and `FieldOffset(8)` — same cache line)
and **padded** (`FieldOffset(0)` and `FieldOffset(128)` — separate cache lines, covers 64/128-byte lines +
adjacent-line prefetch). A layout toggle selects which the run uses. Exactly two worker threads, one pinned
per counter, each incrementing its own field in a tight loop for the window. Report total ops/sec per
layout and cache the last packed and padded results to draw a **two-bar contrast**. Help text: packed
counters silently serialize on cache-coherency (the cliff); padded run frees them; note the magnitude is
hardware/JIT dependent.

## Traceability
- **requirements:** REQ-010, REQ-011
- **acceptance:** AC-005

## Files allowed to touch
- Assets/Scripts/Scenarios/AtomicMasteryScenario.cs (extend)

## Acceptance
- [ ] Packed (`FieldOffset` 0/8) and padded (`FieldOffset` 0/128) layouts via `[StructLayout(LayoutKind.Explicit)]`; only blittable `long` fields, non-overlapping.
- [ ] Layout toggle; a run uses two threads, one per counter, for the window; total ops/sec reported.
- [ ] Padded total ops/sec noticeably higher than packed (the cliff), shown as a two-bar contrast of last results.
- [ ] Help text notes the result is hardware/JIT dependent.
- [ ] Workers touch only their counter field; results read on the main thread.
- [ ] Compiles clean (G2).
