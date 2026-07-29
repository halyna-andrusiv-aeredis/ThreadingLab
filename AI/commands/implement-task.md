# Implement Task

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `AI/project/prompts/developer.md` (also available as the `unity-csharp` skill)
- `$ARGUMENTS`

Act as the DEVELOPER role. Implement **only** the task file passed in `$ARGUMENTS`.

## Arguments

```text
/implement-task fishing-flow-ab-test/tasks/TASK_01.md
```

Or full path: `AI/features/fishing-flow-ab-test/tasks/TASK_01.md`

If `$ARGUMENTS` is empty, ask for the task path before continuing.

## Derive feature context

From task path `AI/features/<feature-id>/tasks/TASK_NN.md`:
- Spec: `AI/features/<feature-id>/spec.md` (reference only — see below)
- Plan: `AI/features/<feature-id>/plan.md` (reference only — see below)
- Status: `AI/features/<feature-id>/status.yaml`

The task file is self-contained (Goal, Traceability, Files allowed to touch, Acceptance) —
implement from it directly. Do **not** read `plan.md`; it is a superset of what's already
distilled into this task. Only open `spec.md`, and only the section(s) for the specific
`REQ-*`/`AC-*` IDs listed under Traceability, if the task's own Acceptance checklist is
ambiguous about what those requirements mean. Implement **only** what the task requires.

## Scope rules

- Touch **only** files under **Files allowed to touch**
- No prefabs, scenes, `.meta`, `ProjectSettings/`, `Packages/`, Addressables unless task allows
- Keep classic/control behavior unchanged unless task explicitly changes it

## After implementation

Output: changed files, why, summary, acceptance checklist, risks, notes for reviewer.
Do not start the next task unless the user asks.
