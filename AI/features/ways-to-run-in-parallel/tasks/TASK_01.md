# Task 01 — Scenario skeleton + texture pipeline + Sequential method

## Goal
Create `WaysToRunInParallelScenario`, register it, and render an animated `sin/cos` "creature"
field into a reused `Texture2D` every frame using the **Sequential** (main-thread) method, with
metrics (method, compute ms, FPS, resolution) and a texture destroyed on `Exit()`.

## Traceability
- **requirements:** REQ-001, REQ-002, REQ-004, REQ-007 (single method), REQ-008 (main-thread upload), REQ-009, REQ-011, REQ-012
- **acceptance:** AC-001, AC-002, AC-003

## Files allowed to touch
- Assets/Scripts/Scenarios/WaysToRunInParallelScenario.cs (new)
- Assets/Scripts/Core/ThreadingLabHost.cs (add one registration line in BuildScenarios)

## Acceptance
- [ ] Scenario appears in the picker and is selectable.
- [ ] A red creature/field animates over time (recomputed each frame from `Time`/`_time`).
- [ ] `Sample(x,y,time)` is deterministic; the field is written into a reused `Color32[]` buffer
      and uploaded to a reused `Texture2D` (`SetPixels32` + `Apply`) on the main thread only.
- [ ] Sequential compute runs on the main thread; raising resolution visibly drops FPS.
- [ ] Metrics show method, compute ms/frame, FPS, resolution.
- [ ] `Exit()` destroys the `Texture2D` (no leak); buffer/texture reused across frames.
- [ ] IMGUI styles cached (no per-frame `new GUIStyle`).
- [ ] Compiles clean (G2).
