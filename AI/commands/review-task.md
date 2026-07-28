# Review Task

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `AI/project/prompts/reviewer.md` (also available as the `reviewer` skill)
- `$ARGUMENTS`

Act as the REVIEWER role. **Judge** only what this diff introduces or exposes — but you may
read beyond the diff (touched files in full, call sites, interface implementers, DI graph) to
assess impact (blast radius). Do not flag pre-existing issues the change did not touch.

If `/build-feature` ran **Compile gate (G2)** before this review, read the latest `AI/artifacts/unity-compile-*.log` (or path from implement step). Do **not** claim "project compiles" from IDE/linter alone when compile gate was skipped or failed.

## Arguments

```text
/review-task fishing-flow-ab-test/tasks/TASK_01.md
```

Or: `AI/features/fishing-flow-ab-test/tasks/TASK_01.md`

If `$ARGUMENTS` is empty, ask for the task path before continuing.

## Paths

From `AI/features/<feature-id>/tasks/TASK_NN.md`:
- Spec: `AI/features/<feature-id>/spec.md`
- Review file (only when findings exist): `AI/features/<feature-id>/reviews/REVIEW_TASK_NN.md`
- Status: `AI/features/<feature-id>/status.yaml`

## Fix policy — the reviewer does not edit code

- The reviewer **reports** findings; it does **not** apply fixes or commit.
- Each Must-fix carries a **suggested minimal fix**; the developer applies it via
  re-implement (task `blocked → in_progress`), then G2 recompile + re-review.
- Suggest the smallest change that removes the defect; do not expand scope.

## Review outcome — record, don't archive

A review is a **gate outcome**, not a mandatory document. Record the verdict on the
task in `status.yaml`; write a review **file only when there is something to persist**.

Decide by findings:

| Findings | `status.yaml` task `review:` | Write `REVIEW_TASK_NN.md`? |
|----------|------------------------------|----------------------------|
| None (clean approve) | `passed` | **No file.** The `passed` value is the record. |
| Must fix present | `reviews/REVIEW_TASK_NN.md` | **Yes** — must-fix + should-fix + *suggested* fixes. Task `→ blocked` (Gate G1). |
| Should fix / Nice to have only (approved) | `reviews/REVIEW_TASK_NN.md` | **Yes** — persist the deferred items so they aren't lost. |

Rules:
- Never leave a task with no `review` value after a review runs.
- Nice-to-have-only findings may be folded into the approve note; a file is optional there.
- A review file, when written, contains: task path, verdict, diff scope, acceptance
  vs diff, Must fix / Should fix / Nice to have, and the suggested fixes.

Output briefly: verdict, `review:` value written, must-fix items (if any), next step.
