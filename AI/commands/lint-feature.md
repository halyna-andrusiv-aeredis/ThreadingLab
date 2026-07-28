# Lint Feature

Run mechanical invariant checks on a feature folder. `/build-feature` runs this automatically after tasks exist (`--new` step 6, `--resume` preflight). Use this command for a manual check or before closing QA.

## Arguments

```text
/lint-feature fishing-flow-ab-test
```

If `$ARGUMENTS` is empty, ask for the feature id.

## Run

From repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File AI/scripts/lint-feature.ps1 -FeatureId <feature-id>
```

Use `-Strict` to treat warnings as errors.

## What it checks

### Errors (must fix)
1. Required files: `spec.md`, `plan.md`, `status.yaml`, `feature.yaml`
2. Every `tasks/TASK_NN.md` has a matching entry in `status.yaml`
3. Every `status.yaml` task has a matching `TASK_NN.md` file
4. **Code** tasks with `status: done` have the review file on disk
5. `last_processed_change` is not behind the newest `decisions/CR-*.md`

### Warnings (optional)
- `spec.md` missing REQ-* / AC-* IDs
- REQ not referenced in any task `## Traceability` (legacy tasks OK)

## After running

Output pass/fail summary. If errors, list fixes (e.g. add review, sync status.yaml, complete CR pipeline).

Recommend manual run when:
- Verifying state without starting `/build-feature`
- Marking feature `overall: done`

Normally **not** needed before `--new` or `--resume` — orchestrator runs lint for you.

Do not modify code or feature docs unless user asks to fix lint failures.
