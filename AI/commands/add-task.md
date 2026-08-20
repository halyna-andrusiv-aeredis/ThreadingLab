# Add Task

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`
- `AI/features/<feature-id>/plan.md` — if `--task` is given, read **only that task's entry**;
  if omitted (auto-pick), scan just the task headers/numbers to find the target, then read
  that one entry in full. Either way, never digest the whole plan's task bodies.
- `AI/features/<feature-id>/spec.md` — **only its REQ-*/AC-* ID list**, not the full narrative.
- `AI/features/<feature-id>/tasks/` — **list file names only** (for numbering — see "Picking
  the task number" below); do not open their content unless you need the immediately
  preceding task's Files-allowed-to-touch to gauge scope/conventions.
- `AI/project/prompts/architect.md`

Reading every existing task file in full on every invocation doesn't scale — a feature with
15 tasks would cost ~2k+ tokens of content this command never uses (it only needs numbers).

Act as the ARCHITECT role. Create **one** task file — normally by materializing an existing
entry in `plan.md`; for a bug-fix regression task appended via `/fix-bug` Step 3A, no plan
entry may exist yet (see "No plan entry" below). Do not write code.

## Arguments

```text
/add-task fishing-flow-ab-test --task 13
```

Or plan path + optional `--task NN`.

## Paths

From `<feature-id>`:
- Plan: `AI/features/<feature-id>/plan.md`
- Spec: `AI/features/<feature-id>/spec.md`
- Output: `AI/features/<feature-id>/tasks/TASK_NN.md`

If `$ARGUMENTS` is empty, ask for the feature id before continuing.

## Picking the task number

- **Plan entry exists** (normal case — after `/split-tasks` or `/update-plan`): if `--task`
  is omitted, use the highest plan task number that has no task file yet.
- **No plan entry** (bug-fix regression task via `/fix-bug` Step 3A): `plan.md` has nothing
  to number this from — `--task` must be given explicitly; use the next number after the
  highest existing `tasks/TASK_NN.md` file.

## Output

`AI/features/<feature-id>/tasks/TASK_NN.md`, using `AI/templates/task.template.md`.

- **Plan entry exists** — copy Goal / Files-allowed-to-touch / Acceptance from that plan
  entry. Include **Traceability** (`requirements`, `acceptance`) when IDs exist in `spec.md`.
- **No plan entry** (bug-fix regression) — there is nothing in `plan.md` to copy from: build
  Goal/Acceptance from the bug's `violated_ac` and `spec.md` directly; set **Traceability →
  acceptance** to that `violated_ac`; keep Files-allowed-to-touch minimal. Also append a
  matching one-line entry to `plan.md` for this task, so the plan stays the complete task
  list for the feature instead of silently falling out of sync.

Rules:
- Do not overwrite existing task files unless user explicitly asked
- Do not write code
- Validation tasks: `Files allowed to touch: None`

Suggest: `/implement-task fishing-flow-ab-test/tasks/TASK_NN.md`
