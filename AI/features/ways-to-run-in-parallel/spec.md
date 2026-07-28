# Feature: Ways to Run in Parallel scenario

## Changelog
- **2026-07-24:** Initial spec.
- **2026-07-24 (CR-01):** Payload switched from a per-pixel `sin/cos` field to the authentic
  @yuruyurau **particle system** (point cloud, additive accumulation). Parallel methods now
  partition **points** and use **per-thread accumulation buffers + a main-thread merge** (no shared
  write). Weight control is **point count** (250k/1M/2M/4M); canvas is fixed 512×512. See
  `decisions/CR-01-particle-creature.md`.
- **2026-07-24 (CR-02):** Performance pass. Per-point cache of the time-independent formula parts
  (3 trig/point/frame instead of 8), `MathF`, canvas 512→400, white creature, responsive Frame-ms
  metric, default 250k; and **parallelized the merge + colorize** (one `Parallel.For` over disjoint
  pixel ranges) with `SetPixelData` upload. See `decisions/CR-02-performance-pass.md`.

## Problem
The matrix asks for "knows the different ways to run code in parallel *and the downside of
each*". A learner needs to feel the difference, not read about it. An embarrassingly-parallel,
visual workload makes the trade-offs obvious.

## Goal
A new `IThreadingScenario` — "Ways to Run in Parallel" — that renders an **animated, pulsating
"creature"** field (pure `sin/cos` math per pixel) into a `Texture2D`, recomputed every frame.
The same per-frame workload can be run three ways, chosen live:
**Sequential (main thread) / `Parallel.For` / `ThreadPool`**. The user watches FPS and
compute-time-per-frame change as they switch method and increase the workload weight.

Demonstrates matrix cells: **the different ways to run code in parallel + the downside of each**,
the **ThreadPool** and its manual use, and — again — that a `Texture2D` upload is main-thread-only.

## Non-goals
- No GPU: no shaders, Compute Shaders, or `Graphics.Blit`. Pure CPU `System.Threading` (the point).
- No Unity Job System / Burst / `NativeArray`.
- No async double-buffering — the per-frame compute is synchronous (the main thread waits for the
  chosen method to finish before applying the texture). Truly-off-thread rendering is out of scope.
- Not adding raw-`Thread` or `Task.Run+WhenAll` methods now (possible later "nice to have").

## User scenarios
### Scenario A — Switch the method, watch the cost
1. User selects "Ways to Run in Parallel". A red creature pulsates on screen.
2. User raises the **workload weight** (resolution / per-pixel iterations) until FPS visibly drops
   on **Sequential**.
3. User switches to **Parallel.For** → FPS jumps up, compute-ms/frame drops roughly by the core count.
4. User switches to **ThreadPool** → similar speedup; comparable numbers.

## Functional requirements

### REQ-001 — Scenario registered and selectable
`WaysToRunInParallelScenario : IThreadingScenario` under `Assets/Scripts/Scenarios/`, registered
in `ThreadingLabHost.BuildScenarios()`, appears in the picker.

### REQ-002 — Animated particle creature to a texture
Every frame the scenario maps N points through the @yuruyurau trig function to screen positions and
**accumulates** them (additive splat) into a brightness buffer, mapped to a red-orange `Texture2D`
(fixed 512×512). A time parameter animates it; the pulsating "creature" is the accumulated cloud.

### REQ-003 — Method selector (live)
A selector chooses the compute method: **Sequential / Parallel.For / ThreadPool**. Switching takes
effect on the next frame with no restart.

### REQ-004 — Sequential method
Computes all pixels in a single loop on the main thread (the baseline / slow path).

### REQ-005 — Parallel.For method
Partitions the **points** into P ranges (P = core count) via `System.Threading.Tasks.Parallel.For`;
each range accumulates into its **own** buffer; the main thread waits, then merges the buffers.

### REQ-006 — ThreadPool method
Partitions the points into P ranges over `ThreadPool.QueueUserWorkItem`, each writing its **own**
buffer, waited via `CountdownEvent`; the main thread then merges.

### REQ-007 — Identical output across methods
For the same time value, all three methods produce the **identical** image (same math + additive
merge is order-independent) — only the compute time differs.

### REQ-008 — No shared-write race; main-thread merge & upload
No two workers write the same buffer: each accumulates into its **own** per-thread buffer, and the
main thread merges them (addition is commutative, so the result is deterministic). The merge and
`texture.SetPixels32/Apply` run **only on the main thread**.

### REQ-009 — Metrics display
Shows: selected method, compute time per frame (ms), current FPS, resolution, and worker/partition count.

### REQ-010 — Workload-weight control
A control sets the **point count** (250k / 1M / 2M / 4M) so the user can make the work heavier and
watch the parallel speedup — and Sequential's downside — become visible. Canvas stays 512×512.

### REQ-011 — Clean teardown
`Exit()` releases the `Texture2D` (`Object.Destroy`) and stops using workers — re-entering does not
leak textures or threads.

### REQ-012 — No per-frame churn
IMGUI styles cached (built once); the pixel buffer and texture are reused across frames (reallocated
only when the resolution changes), consistent with the project rule on `DrawGUI`/per-frame allocations.

## Failure scenarios
- Changing resolution mid-run reallocates the buffer + texture safely (old texture destroyed).
- Very high workload weight drops FPS a lot on Sequential — expected, that is the demonstrated downside.

## Analytics
- none.

## Data / persistence
- none.

## Platform constraints
- Editor / Standalone only. WebGL is out of scope (single-threaded). See `AI/profile.yaml → platforms`.

## UX / UI
- The rendered creature texture (point-filtered) + a method selector + a weight control + the metrics line.

## Acceptance criteria

### AC-001 — Selectable
- **Given** Play mode
- **When** the user opens the picker
- **Then** "Ways to Run in Parallel" is listed and selecting it shows the animated texture + controls.

### AC-002 — Animates
- **Given** the scenario is active
- **When** no interaction happens
- **Then** the red creature/field visibly pulsates (animates over time).

### AC-003 — Sequential is the slow baseline
- **Given** the workload weight is raised
- **When** the method is Sequential
- **Then** FPS drops and compute-ms/frame is high.

### AC-004 — Parallel.For speeds it up
- **Given** the same weight
- **When** the user switches to Parallel.For
- **Then** compute-ms/frame drops noticeably (roughly toward sequential / core-count) and FPS rises.

### AC-005 — ThreadPool speeds it up
- **Given** the same weight
- **When** the user switches to ThreadPool
- **Then** compute-ms/frame and FPS are in the same improved ballpark as Parallel.For.

### AC-006 — Identical image, live switch
- **Given** any method
- **When** the user switches method
- **Then** the image shape is identical across methods (only speed/metrics change) with no restart.

### AC-007 — No cross-thread errors
- **Given** any parallel method running
- **When** the frame is produced
- **Then** no "can only be called from the main thread" error and no unhandled exception appears in the Console.

### AC-008 — No leak on re-entry
- **Given** the scenario has been used
- **When** the user switches away and back several times
- **Then** texture count and process-thread count do not grow.

## Open questions
- [ ] Weight control: resolution steps (e.g. 128/256/512/768) vs. per-pixel iteration count vs. both.
      Recommended: resolution steps (simple, and directly changes pixel count = parallel work). Resolve at plan time.
- [ ] ThreadPool wait primitive: `CountdownEvent` vs `ManualResetEventSlim` + `Interlocked`.
      Recommended: `CountdownEvent` (clearest intent). Resolve at plan time.
