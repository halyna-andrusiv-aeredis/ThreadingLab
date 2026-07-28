# Split Plan Into Tasks

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`
- `AI/project/prompts/architect.md`

Act as the ARCHITECT role. Split plan into task files. Do not write code.

## Arguments

```text
/split-tasks fishing-flow-ab-test
```

Or plan path: `AI/features/fishing-flow-ab-test/plan.md`

## Paths

From `<feature-id>`:
- Plan: `AI/features/<feature-id>/plan.md`
- Tasks: `AI/features/<feature-id>/tasks/TASK_01.md`, `TASK_02.md`, …

If `$ARGUMENTS` is empty, ask for the feature id before continuing.

## Task file template

Use `AI/templates/task.template.md`:

```md
# Task <NN> — <Short title>

## Goal
…

## Traceability
- **requirements:** REQ-001
- **acceptance:** AC-002

## Files allowed to touch
…

## Acceptance
…
```

When splitting from plan, copy **REQ-* / AC-*** IDs from `spec.md` into each task. Validation tasks may omit traceability or list AC IDs they verify manually.

## Rules

- Keep each task small and independently reviewable
- Strict allow-list from plan
- Preserve plan task order
- List created files after completion
