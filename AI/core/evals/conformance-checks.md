# Conformance checks (deterministic)

Mechanical yes/no checks derived from [`../handoffs.md`](../handoffs.md) and
[`../state-machine.md`](../state-machine.md). Any command's output can be graded against
the relevant subset with no judgement calls. These are the first candidates to script into
`AI/scripts/lint-feature.ps1`.

## Plan (`plan.md`) — from Architect
- [ ] Every task lists **Goal**.
- [ ] Every task lists **Files affected**.
- [ ] Every task lists **Type** (Code / Config / Prefab / Scene / Addressables / Tests).
- [ ] Every task lists **Expected result** and **Validation/check**.
- [ ] Every task lists **Rollback risk** (Low / Medium / High).
- [ ] Every task lists **Traceability** referencing at least one `REQ-*` or `AC-*`.
- [ ] Every `REQ-*` / `AC-*` in the spec is covered by ≥1 task (no orphan requirements).

## Task files (`tasks/TASK_NN.md`) — from Split-tasks
- [ ] Each has a single **Goal**, a **Files allowed to touch** list, and **Acceptance**.
- [ ] Traceability block present (or explicitly marked validation-only).

## Status (`status.yaml`) — state machine
- [ ] `overall` ∈ {not_started, planned, tasks_split, implementing, qa_pending, done, blocked}.
- [ ] Every task `status` ∈ {pending, in_progress, blocked, done, superseded}.
- [ ] `qa_pending` only if no code task is pending/in_progress/blocked.
- [ ] `done` only if every task is done/superseded.
- [ ] `blocked` iff ≥1 task is blocked (or a recorded blocker exists).
- [ ] Each reviewed task has a `review` value (`passed` or a `REVIEW_TASK_NN.md` path).

## Review outcome — Step 5 policy
- [ ] Clean approve → `review: passed`, **no file**.
- [ ] Findings present → `REVIEW_TASK_NN.md` exists and is referenced from `status.yaml`.

## Diff scope — Developer → Reviewer handoff
- [ ] Changed files ⊆ the task's **Files allowed to touch** (else Must-fix scope violation).
- [ ] No edits to `profile.protected_paths` unless the task explicitly allows it.
