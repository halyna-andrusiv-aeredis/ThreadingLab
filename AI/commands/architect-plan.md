# Architect Plan

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`
- `AI/project/prompts/architect.md`

Act as the ARCHITECT role defined in `AI/project/prompts/architect.md`.

Create an implementation plan only. Do not write code or modify Unity assets.

## Arguments

Feature id (preferred):
```text
/architect-plan fishing-flow-ab-test
```

Or spec path: `AI/features/fishing-flow-ab-test/spec.md`

## Paths

From `<feature-id>`:
- Spec: `AI/features/<feature-id>/spec.md`
- Plan output: `AI/features/<feature-id>/plan.md`

If `$ARGUMENTS` is empty, ask for the feature id before continuing.

When creating a new spec, use `AI/templates/spec.template.md` (REQ-* / AC-* IDs). Plan sections may reference requirement IDs in prose, e.g. "implements REQ-004".
