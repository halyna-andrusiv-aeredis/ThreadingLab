# Plan: Main Thread Freeze scenario

Traces `spec.md` (REQ-001..010, AC-001..007). Stack per `AI/profile.yaml`: no DI, IMGUI,
raw `System.Threading`/TPL, QA target Editor/Standalone (not WebGL).

## 1. Architecture proposal

A single new scenario class, no shell/infrastructure changes beyond one registration line.

- **`MainThreadFreezeScenario : IThreadingScenario`** (new, `Assets/Scripts/Scenarios/`).
  - Holds UI/run state: `volatile bool _running`, `Mode _lastMode`, `long _lastResult`,
    `double _lastMs`, `float _minFpsDuringRun`, `string _status`.
  - `Tick(dt)`: advances the liveness indicator phase from `Time.unscaledTime`; while
    `_running`, samples the frame delta to track worst FPS (min-FPS-during-run, REQ-008).
    (During a main-thread run `Tick` cannot fire — the one huge frame after unblocking yields
    the ~0 FPS reading, which is exactly the point.)
  - `DrawGUI`: cached IMGUI styles (REQ-010); animating bar; two buttons disabled while
    `_running` (REQ-007); result/metrics block.
  - `Workload()`: deterministic compute of fixed size (e.g. count primes ≤ N) → returns a
    `long`; identical for both modes (REQ-006).
  - Main-thread run: call `Workload()` synchronously in the button handler (REQ-003).
  - Background run: `Task.Run(Workload)`, then `MainThreadDispatcher.Instance.Enqueue(...)`
    to write results back on the main thread (REQ-004, REQ-005); guard a late callback after
    `Exit()` (REQ-009).
- **`ThreadingLabHost.BuildScenarios()`** (change): add `_scenarios.Add(new MainThreadFreezeScenario());`.

Reuses existing infra: `MainThreadDispatcher` (marshaling), the host's metrics strip (global
FPS). No new patterns, no new dependencies.

## 2. Dependency / impact analysis

- Depends on: `IThreadingScenario`, `MainThreadDispatcher` (both exist and are stable).
- Blast radius: the only shell edit is one `Add(...)` line in `BuildScenarios()` — additive,
  cannot affect the existing Race Condition scenario. Everything else is self-contained in the
  new file. No shared mutable state introduced.

## 3. Files to create / change

- **Create:** `Assets/Scripts/Scenarios/MainThreadFreezeScenario.cs`
- **Change:** `Assets/Scripts/Core/ThreadingLabHost.cs` (one registration line in `BuildScenarios`)

## 4. Risks

- **Freeze duration vs. machine speed.** A fixed iteration count runs faster/slower on
  different CPUs. Mitigation: size the workload to ~2–3 s on a typical dev machine and accept
  1–4 s variance; a deterministic result is worth more than an exact duration. (Resolves spec
  open question #1 → fixed-size deterministic compute.)
- **Min-FPS-during-run semantics.** For the main-thread mode the value comes from the single
  long post-block frame; documented as intended, not a bug.
- **Editor "unresponsive" warning.** A ~2.5 s main-thread block may make the Editor briefly
  unresponsive — that is the demonstrated behavior, acceptable in Editor/Standalone.

## 5. Step-by-step implementation plan (proposed task split)

### TASK_01 — Scenario skeleton + main-thread (freezing) path
- **Goal:** New `MainThreadFreezeScenario` registered and selectable; animating indicator;
  cached styles; deterministic `Workload()`; "Run on Main Thread" runs it synchronously;
  result + elapsed time shown; re-entrancy guard.
- **Files:** `Assets/Scripts/Scenarios/MainThreadFreezeScenario.cs` (new),
  `Assets/Scripts/Core/ThreadingLabHost.cs` (register).
- **Type:** Code
- **Expected result:** Selecting the scenario shows an animating bar; "Run on Main Thread"
  freezes it and drops FPS to ~0, then shows result + ms.
- **Validation/check:** G2 compile; visually confirm freeze in Play mode.
- **Traceability:** REQ-001, 002, 003, 006, 007, 008 (main-thread half), 010; AC-001, 002, 003, 005.
- **Rollback risk:** Low (additive; one shell line).

### TASK_02 — Background (non-freezing) path + marshaling + min-FPS
- **Goal:** "Run on Background" via `Task.Run`; result marshaled through `MainThreadDispatcher`;
  min-FPS-during-run tracked in `Tick`; `Exit()` guards a late callback.
- **Files:** `Assets/Scripts/Scenarios/MainThreadFreezeScenario.cs` (extend).
- **Type:** Code
- **Expected result:** "Run on Background" keeps the bar animating and FPS high; result appears
  on completion; no cross-thread Console errors.
- **Validation/check:** G2 compile; visually confirm smooth run + no Console errors.
- **Traceability:** REQ-004, 005, 008 (background half), 009; AC-004, 006, 007.
- **Rollback risk:** Low (same new file).

### TASK_03 — Manual QA (validation)
- **Goal:** Verify AC-001..007 in Play mode (freeze visible, background smooth, no re-entrancy,
  no leaked worker on scenario switch).
- **Files:** none (manual).
- **Type:** validation
- **Expected result:** All ACs pass.
- **Validation/check:** `AI/features/main-thread-freeze/qa/manual.md` at Gate G4.
- **Rollback risk:** n/a.
