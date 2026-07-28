# Plan: Ways to Run in Parallel scenario

> **CR-01 (2026-07-24):** payload is now the authentic @yuruyurau **particle system** (point cloud,
> additive accumulation), not a per-pixel field. Parallel methods partition **points** and use
> **per-thread accumulation buffers + a main-thread merge**. Weight = point count; canvas fixed
> 512×512. See `decisions/CR-01-particle-creature.md`.

Traces `spec.md` (REQ-001..012, AC-001..008). Stack per `AI/profile.yaml`: no DI, IMGUI, raw
`System.Threading`/TPL, QA target Editor/Standalone (not WebGL). CPU only — no GPU/Job System.

## 1. Architecture proposal

One new scenario class + one registration line. Reuses the existing shell and `MainThreadDispatcher`
pattern only implicitly (compute is synchronous per frame, so no async marshaling is needed — the
main thread waits for the chosen method, then applies the texture on the main thread).

- **`WaysToRunInParallelScenario : IThreadingScenario`** (new, `Assets/Scripts/Scenarios/`).
  - Owns: `Texture2D _tex`, `Color32[] _buffer`, `int _res`, `float _time`, `Method _method`,
    metrics (`double _computeMs`, cached FPS from frame delta), cached IMGUI styles.
  - `Tick(dt)`: `_time += dt`; compute the field for `_time` using `_method`; upload to `_tex`
    (`SetPixels32` + `Apply`) — all on the main thread. Track `_computeMs` via `Stopwatch` around
    the compute (not the upload).
  - `DrawGUI`: draw `_tex` (`GUI.DrawTexture`, point filter); method selector (3 buttons/toggle);
    weight control (resolution steps); metrics line.
  - **Field function** `Sample(x, y, time) -> Color32`: layered `sin/cos` of radius/angle/time →
    red-ish intensity (the "creature"). Deterministic in `(x,y,time)`; identical across methods (REQ-007).
  - **Compute methods** — partition the N points into P ranges (P = core count); each range
    accumulates into its **own** `int[]` buffer (no shared write, REQ-008); the main thread merges
    the P buffers into `_accum`:
    - `Sequential` — one main-thread loop over all points into `_accum`.
    - `Parallel.For` — `Parallel.For(0, P, p => PlotRange(range[p], _threadAccum[p]))`; blocks caller; then merge.
    - `ThreadPool` — `QueueUserWorkItem` per range into `_threadAccum[p]`, `CountdownEvent` waits; then merge.
  - **Point map** `MapPoint(i, t)` ports the @yuruyurau trig (k/e/d/q/c → px,py); a one-time
    auto-fit (bbox of a subsample at t=0, 80% canvas) centers the creature. Accumulator → red-orange
    via a saturating brightness curve.
  - `Enter()`: allocate `_buffer`/`_tex` for `_res`. `ResizeIfNeeded()` on weight change.
  - `Exit()`: `Object.Destroy(_tex)`, null the buffer (REQ-011).

- **`ThreadingLabHost.BuildScenarios()`** (change): add `_scenarios.Add(new WaysToRunInParallelScenario());`.

Resolved open questions: weight = **resolution steps** (128/256/512/768); ThreadPool wait = **`CountdownEvent`**.

## 2. Dependency / impact analysis

- Depends on `IThreadingScenario` (stable). No dependency on `MainThreadDispatcher` (synchronous compute).
- Blast radius: one additive `Add(...)` line in the shell; the rest is a self-contained new file. No
  shared mutable state with other scenarios. The only Unity-object lifetime to manage is `_tex`
  (created in `Enter`, destroyed in `Exit`, re-created on resize).

## 3. Files to create / change

- **Create:** `Assets/Scripts/Scenarios/WaysToRunInParallelScenario.cs`
- **Change:** `Assets/Scripts/Core/ThreadingLabHost.cs` (one registration line)

## 4. Risks

- **Texture leak.** `Texture2D` is a Unity object; must `Destroy` on `Exit` and on resize, or every
  scenario switch leaks one. Mitigation: `ResizeIfNeeded` + `Exit` both destroy the old texture. (AC-008.)
- **Parallel speedup only visible when work is heavy enough.** At low resolution the per-frame work is
  tiny and threading overhead dominates (parallel can be *slower*) — this is a real "downside" to show,
  but the default weight should be high enough that AC-004/005 pass. Mitigation: default to 512, and the
  weight control lets the user push higher.
- **Per-frame allocations.** Reuse `_buffer` and `_tex`; only reallocate on resolution change. Cache styles.
- **False-ish sharing note.** Row-band partitioning keeps worker writes far apart in memory; not a
  correctness issue (disjoint), and a deliberate contrast to a later "false sharing" scenario.

## 5. Step-by-step implementation plan (proposed task split)

### TASK_01 — Scenario skeleton + texture pipeline + Sequential
- **Goal:** New `WaysToRunInParallelScenario` registered and selectable; reused `Texture2D`+buffer
  pipeline; the `sin/cos` creature field; **Sequential** compute; animates every frame; metrics
  (method, compute ms, FPS, resolution); cached styles; `Exit()` destroys the texture.
- **Files:** `Assets/Scripts/Scenarios/WaysToRunInParallelScenario.cs` (new),
  `Assets/Scripts/Core/ThreadingLabHost.cs` (register).
- **Type:** Code
- **Expected result:** A pulsating red creature animates (single-threaded); raising resolution drops FPS.
- **Validation/check:** G2 compile; visually confirm animation + Sequential slowdown at high res.
- **Traceability:** REQ-001, 002, 004, 007 (single method), 008 (main-thread upload), 009, 011, 012; AC-001, 002, 003.
- **Rollback risk:** Low (additive; one shell line).

### TASK_02 — Parallel.For + ThreadPool methods + selector + weight control
- **Goal:** Add `Parallel.For` and `ThreadPool` (+`CountdownEvent`) methods over disjoint row bands;
  live method selector; resolution-step weight control; worker/partition count in metrics.
- **Files:** `Assets/Scripts/Scenarios/WaysToRunInParallelScenario.cs` (extend).
- **Type:** Code
- **Expected result:** Switching to Parallel.For / ThreadPool lowers compute-ms/frame and raises FPS at
  the same weight; identical image across methods.
- **Validation/check:** G2 compile; visually compare FPS/ms across the three methods at high weight.
- **Traceability:** REQ-003, 005, 006, 010; AC-004, 005, 006, 007, 008.
- **Rollback risk:** Low (same new file).

### TASK_03 — Manual QA (validation)
- **Goal:** Verify AC-001..008 in Play mode (animates; Sequential slow; parallel faster; identical
  image; no Console errors; no leak on re-entry).
- **Files:** none (manual).
- **Type:** validation
- **Traceability:** AC-001..008.
- **Rollback risk:** n/a.
