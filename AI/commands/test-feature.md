# Test Feature

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `AI/project/prompts/tester.md` (also available as the `tester` skill)
- `$ARGUMENTS`

Act as the TESTER role. Produce a **feature-wide manual QA plan**.

## Arguments

```text
/test-feature fishing-flow-ab-test
```

Or: `AI/features/fishing-flow-ab-test/spec.md`

If `$ARGUMENTS` is empty, ask for the feature id before continuing.

## Paths

From `<feature-id>`:
- Spec: `AI/features/<feature-id>/spec.md`
- Plan: `AI/features/<feature-id>/plan.md`
- Tasks: `AI/features/<feature-id>/tasks/TASK_*.md`
- Reviews: `AI/features/<feature-id>/reviews/REVIEW_TASK_*.md`
- Decisions: `AI/features/<feature-id>/decisions/CR-*.md`
- Status: `AI/features/<feature-id>/status.yaml`
- Output: `AI/features/<feature-id>/qa/manual.md`

## Goal

Feature-wide manual QA checklist — not per-task. Do not run Unity automatically.

## What to read

1. **spec.md** — REQ-*, AC-* (authoritative acceptance)
2. **plan.md**, **tasks/**, **reviews/**, **decisions/**
3. Task **Traceability** blocks — map tasks to REQ/AC

## Test plan rules

- Tag checks with `[AC-00N]` when spec has numbered acceptance criteria
- One high-signal row per AC where practical; group edge cases under related AC
- Cover Classic regression (AC-002) and experiment path separately

## Preserve existing results

If `qa/manual.md` exists:
- Keep rows where **Actual** / **Pass** are already filled
- Add new checks from CR / spec changes
- Mark removed checks deprecated; do not delete filled results

## After creating

Output: path to `qa/manual.md`, check counts by section, gaps, reminder to test manually in Unity.
