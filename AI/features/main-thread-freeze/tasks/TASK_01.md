# Task 01 — Scenario skeleton + main-thread (freezing) path

## Goal
Create `MainThreadFreezeScenario`, register it in the host, and implement the main-thread
(blocking) run so the freeze is visible: an animating indicator, a deterministic workload,
"Run on Main Thread" running it synchronously, result + elapsed time, and a re-entrancy guard.

## Traceability
- **requirements:** REQ-001, REQ-002, REQ-003, REQ-006, REQ-007, REQ-008 (main-thread half), REQ-010
- **acceptance:** AC-001, AC-002, AC-003, AC-005

## Files allowed to touch
- Assets/Scripts/Scenarios/MainThreadFreezeScenario.cs (new)
- Assets/Scripts/Core/ThreadingLabHost.cs (add one registration line in BuildScenarios)

## Acceptance
- [ ] Scenario appears in the picker and is selectable.
- [ ] An indicator animates continuously from `Tick`/`Time.unscaledTime` while idle.
- [ ] A deterministic `Workload()` returns a stable `long` result for a fixed input.
- [ ] "Run on Main Thread" runs `Workload()` synchronously on the main thread; the indicator
      visibly freezes and FPS drops to ~0 for the duration.
- [ ] After the run, mode + result + elapsed ms are shown.
- [ ] Both run buttons are disabled while a run is in progress.
- [ ] IMGUI styles are cached (built once), no per-frame `new GUIStyle`.
- [ ] Compiles clean (G2).
