# Update Plan

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`
- Latest CR: `AI/features/<feature-id>/decisions/CR-*.md` (most recent)
- `AI/features/<feature-id>/tasks/TASK_*.md`
- `AI/project/prompts/architect.md`

Act as the ARCHITECT role. Apply spec changes to plan as a **delta only**.

## Arguments

```text
/update-plan fishing-flow-ab-test
```

Or: `AI/features/fishing-flow-ab-test/plan.md`

If `$ARGUMENTS` is empty, ask for the feature id before continuing.

## Rules

- Patch `AI/features/<feature-id>/plan.md` only
- Preserve completed task history; append new tasks (Task 13, 14…)
- Do not create task files or code
- List which `tasks/TASK_*.md` need create/amend

## After completing

Suggest: `/add-task fishing-flow-ab-test --task NN`
