# Threading Lab — project rules (always applies)

Unity **6000.0.66f2 LTS** (Unity 6), Built-in pipeline, C#. **No DI / UniTask / UniRx / Addressables** — this project is deliberately **raw `System.Threading` + TPL** so the concurrency primitives stay visible. UI is **IMGUI** (`OnGUI`), drawn in code (no scene wiring). QA target: **Editor / Standalone** (NOT WebGL).

**Canonical sources** (this file is the always-on digest; the detail lives once, elsewhere):
- Engineering invariants → `AI/core/rules/unity-core.md` (stack-neutral, the single source).
- Concrete stack & project facts → `AI/profile.yaml` (the per-project questionnaire).
- Overview/architecture → `AI/project/context.md`. Read for anything non-trivial.

The rules below are a concise digest of `unity-core.md` — if the two ever diverge, `unity-core.md` wins. **One deliberate inversion:** `unity-core.md`'s WebGL "no threads" constraint does NOT apply here (see `profile.yaml → platforms`); using `Thread`/`Task.Run`/`Parallel.For` is the entire point of this project.

## Hard rules — MUST NOT (without an explicit task)
- Do not modify `ProjectSettings/`, `Packages/manifest.json`, or `.meta` files.
- No large refactors, no mass reformatting, no new package dependencies unless required.
- Do not call Unity API (transforms, meshes, UI, `Time.*`, logging tied to timing) from a worker thread.

## MUST
- Keep changes small and localized; follow existing patterns in the touched module.
- New demos = a new `IThreadingScenario` in `Assets/Scripts/Scenarios/`, registered in `ThreadingLabHost.BuildScenarios()`. Keep the shell (`Core/`) generic.
- **Marshal worker-thread results to the main thread** via `MainThreadDispatcher` — never touch Unity API off-thread.
- Every `Thread` / `Task` a scenario starts is **stopped or joined in `Exit()`** — no worker outlives its scenario (same discipline as subscriptions/handles elsewhere).
- `CancellationToken` must actually stop background work, not just flip a display flag; no silent exception swallowing (surface faults from async code — that is a demo topic here).
- Intentional races/deadlocks/starvation live **inside their own scenario**, reset cleanly, and never corrupt shared lab state.
- Keep per-frame allocations in `DrawGUI` low (IMGUI runs every frame).

## Workflow
Feature/bug work uses the `AI/` pipeline — see `AI/README.md`. Commands are canonical in `AI/commands/`, invoked via `.claude/commands/` (Claude Code) and `.cursor/commands/` (Cursor). Reusable role knowledge lives in `.claude/skills/` (`unity-csharp`, `reviewer`, `tester`, `grill`) and loads on demand in both tools.
