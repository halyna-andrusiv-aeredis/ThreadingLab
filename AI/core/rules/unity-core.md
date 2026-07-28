# Unity engineering invariants (shared, canonical)

Single source of truth for stack-neutral rules that hold for **any** Unity project
using this pipeline. All role prompts (`developer`, `reviewer`, `tester`) and the
always-on project rules reference this file instead of restating it — keep the rules
here and here only.

Roles bind the neutral terms below to concrete tools via [`AI/profile.yaml`](../../profile.yaml):

| Neutral term           | Resolved from                          |
|------------------------|----------------------------------------|
| DI container           | `profile.stack.di`                     |
| Async library          | `profile.stack.async`                  |
| Reactive library       | `profile.stack.reactive`               |
| UI/MVVM framework      | `profile.stack.ui_mvvm`                |
| Asset system           | `profile.stack.assets`                 |
| New-code root / legacy | `profile.code_layout`                  |
| Protected paths        | `profile.protected_paths`              |
| Primary QA target      | `profile.platforms.primary_qa_target`  |

## Guardrails — MUST NOT (without an explicit task)
- Do not change any path in `profile.protected_paths` (ProjectSettings, package
  manifest, `.meta` files, asset addresses/keys, prefabs/scenes/textures/materials).
- No large refactors, no mass reformatting. Keep changes small and localized.
- Do not refactor `profile.code_layout.legacy_zones` without explicit task scope.
- Do not rename serialized fields, public APIs, signals, message types, or asset
  references unless explicitly requested. Preserve backwards compatibility of
  serialized data and inspector bindings.
- No new cross-module dependencies (UI ↔ domain ↔ networking) unless the task requires it.

## Architecture & module boundaries
- Respect existing module boundaries and dependency direction; follow existing
  patterns in the touched module.
- Keep logic out of MonoBehaviours (services/models/view-models). Views stay dumb:
  render + forward input, no domain logic.
- Avoid "shared utils everywhere" unless an established shared module exists.

## Dependency injection (mandatory)
- Register services via the DI container's installers/composition root — no hidden
  singletons and no `FindObjectOfType` in new code.
- Do not `new` services the DI container already manages. Prefer constructor
  injection where the architecture allows; else follow the module's pattern.

## Subscriptions / reactivity (mandatory)
Every subscription has a matching disposal tied to a clear lifetime. Applies to the
reactive library's `Subscribe`, C# events, callbacks, timers, and network handlers.
Acceptable bindings: `CompositeDisposable`, `.AddTo(...)`, `Dispose()` in
`OnDisable`/`OnDestroy`, explicit teardown on deactivation.
No fire-and-forget subscriptions. Do not rely on GC. No dangling subscriptions after
a screen/page/feature/system deactivates.

## Async (mandatory)
- Long-running async supports cancellation tied to object/page lifetime.
- Use the project's async library. Avoid `async void` except at Unity/event entry
  points where unavoidable; prefer the async type over its `Void` variant.
- Fire-and-forget flows handle exceptions explicitly — no silent failures.
- Custom awaitables are allowed when they make call sites cleaner, but each MUST
  honor a `CancellationToken`, dispose every hook it registers (no leaks on cancel
  or completion), and stay small and single-purpose. Check the async library for a
  ready-made awaiter first.

## Asset system (mandatory)
- Every async load/instantiate has a matching release/release-instance tied to a
  clear lifetime — same discipline as subscriptions. No orphaned handles.
- Release on page/state teardown, not "eventually". Do not rely on GC or scene unload.
- Keep asset keys/addresses stable.

## Primary QA target constraints — WebGL (when `profile.platforms.primary_qa_target` = webgl)
- No blocking waits, no threading (`Thread`, `Task.Run`, `Thread.Sleep`,
  `.Wait()`/`.Result`). Use the async library + `await`.
- Audio needs a user-gesture unlock — no playback assumed before first input.
- Mind memory and repeated-open flows (see Subscriptions / Asset system).

## Performance / GC
- Avoid allocations in hot paths; avoid LINQ in frequently executed code; avoid
  closure allocations in per-frame/reactive flows.

## Unity lifecycle
- Avoid logic in `Update` unless necessary; avoid `FindObjectOfType` / repeated
  `GetComponent` in hot paths; respect `Awake/Start/OnEnable/OnDisable/OnDestroy`
  ordering and races.

## Quality bar after each task
- Project compiles; no new Unity Console errors/warnings from the change (Gate G2).
- Subscriptions/events/handlers disposed on teardown; async respects cancellation
  and surfaces exceptions.
- Control/baseline behavior unchanged unless the task says otherwise.
