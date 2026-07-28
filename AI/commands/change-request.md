# Change Request

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS` (feature id on first line, change description below)
- `AI/features/<feature-id>/plan.md`
- `AI/features/<feature-id>/tasks/TASK_*.md`
- `AI/project/prompts/architect.md`

Act as the ARCHITECT role for **requirement changes only**.

## Arguments

```text
/change-request fishing-flow-ab-test

In Flow B, hide the fish health bar. Classic unchanged.
```

Or spec path: `AI/features/fishing-flow-ab-test/spec.md`

## Paths

- Spec: `AI/features/<feature-id>/spec.md`
- Impact report: `AI/features/<feature-id>/decisions/CR-NNN-<short-slug>.md` (next sequential CR number)

## Tasks

1. Update `spec.md`; add `## Changelog` entry; add or amend **REQ-*** / **AC-*** IDs when acceptance changes
2. Mark changed sections inline where helpful
3. Save impact report in `decisions/`
4. Classify impacted tasks: Done / Amend / New work / Obsolete
5. Do **not** modify `plan.md`, `tasks/`, `status.yaml`, or code

## After completing

Output: updated spec path, CR path, next steps:
```text
/update-plan fishing-flow-ab-test
/add-task fishing-flow-ab-test --task NN
/build-feature fishing-flow-ab-test --resume
```

Do **not** set `last_processed_change` in `status.yaml` until CR pipeline completes.
