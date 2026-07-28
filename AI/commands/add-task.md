# Add Task

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`
- `AI/features/<feature-id>/spec.md`
- `AI/features/<feature-id>/tasks/TASK_*.md`
- `AI/project/prompts/architect.md`

Act as the ARCHITECT role. Create **one** task file from the plan.

## Arguments

```text
/add-task fishing-flow-ab-test --task 13
```

Or plan path + optional `--task NN`. If `--task` omitted, use highest plan task number without a file.

## Output

`AI/features/<feature-id>/tasks/TASK_NN.md`

Use `AI/templates/task.template.md`. Include **Traceability** (`requirements`, `acceptance`) when IDs exist in `spec.md`.

- Do not overwrite existing task files unless user explicitly asked
- Do not write code
- Validation tasks: `Files allowed to touch: None`

Suggest: `/implement-task fishing-flow-ab-test/tasks/TASK_NN.md`
