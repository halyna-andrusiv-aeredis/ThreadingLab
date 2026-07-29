# Build Feature

Read always:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`
- `AI/core/state-machine.md` — **the states and legal transitions this command must obey**

Do **not** pre-load the role prompts here. `/architect-plan`, `/implement-task`, and
`/review-task` each declare their own role-prompt read in their own command file — read
`architect.md` / `developer.md` / `reviewer.md` only at the point in this command where that
step actually runs, matching current `overall` (e.g. a `qa_pending` resume needs none of
them; a resume that only continues the Implement loop needs `developer.md`, not
`architect.md`). Pre-loading all three on every invocation wastes ~13k tokens on runs that
touch only one phase.

Orchestrate a feature from spec through plan, tasks, implement/review loop, and feature QA.

If `$ARGUMENTS` is empty, ask for the feature id before continuing.

## Arguments

```text
/build-feature fishing-flow-ab-test
/build-feature fishing-flow-ab-test --new
/build-feature fishing-flow-ab-test --resume
/build-feature fishing-flow-ab-test --resume --all
```

Also accepts full spec path: `AI/features/fishing-flow-ab-test/spec.md`

**Task-loop granularity:** `--one` is the **default** — the Implement loop stops after **one**
code task reaches `done` (Task-loop gate, see below) instead of continuing to the next. Pass
`--all` to run the whole remaining Implement loop without stopping between tasks. Default to
`--one` and start a **fresh chat session** before the next command — see "After completing".

## Resolve feature id

From `$ARGUMENTS`, derive `<feature-id>` (kebab-case slug):
- `fishing-flow-ab-test`
- `AI/features/fishing-flow-ab-test/spec.md` → `fishing-flow-ab-test`

Register in `AI/features/index.yaml` when creating a new feature.

## Paths (from feature id)

- Root: `AI/features/<feature-id>/`
- Spec: `AI/features/<feature-id>/spec.md`
- Plan: `AI/features/<feature-id>/plan.md`
- Tasks: `AI/features/<feature-id>/tasks/TASK_*.md`
- Status: `AI/features/<feature-id>/status.yaml`
- Reviews: `AI/features/<feature-id>/reviews/REVIEW_TASK_NN.md`
- QA manual: `AI/features/<feature-id>/qa/manual.md`
- Decisions: `AI/features/<feature-id>/decisions/CR-*.md`
- Metadata: `AI/features/<feature-id>/feature.yaml`

---

## State machine (authoritative)

`status.yaml → overall` is always one of:
`not_started → planned → tasks_split → implementing → qa_pending → done` (+ `blocked`).
Task `status` is one of: `pending → in_progress → blocked → done` (+ `superseded`).

**Drive every decision from the current state, per `AI/core/state-machine.md`.** Only
the transitions listed there are legal; update `overall` and the task `status` at the
exact points the Gate→transition map specifies. On `--resume`, read `overall` first and
route accordingly:

| `overall`      | `--resume` action                                             |
|----------------|---------------------------------------------------------------|
| `not_started`  | Refuse resume — run `--new`.                                  |
| `planned`      | Run split-tasks → `tasks_split`.                              |
| `tasks_split`  | Enter Implement loop → `implementing`.                       |
| `implementing` | Continue Implement loop from first non-`done` code task.     |
| `blocked`      | Resolve the blocked task first (STOP unless it is now fixed). |
| `qa_pending`   | Remind about manual QA (Gate G4); do not touch done tasks.   |
| `done`         | Nothing to do unless a newer change request exists. Remind about Gate G6 below if the user is about to merge. |

`qa_pending` and `done` resumes need **no role prompt** — do not read `architect.md`,
`developer.md`, or `reviewer.md` for either.

---

## Mode selection

### `--new` (default)

Use only when the feature **does not exist yet**.

**Refuse `--new` if any of these exist:**
- `AI/features/<feature-id>/tasks/TASK_*.md`
- `AI/features/<feature-id>/plan.md`
- `AI/features/<feature-id>/status.yaml`

Tell the user to use `--resume` or the change-request flow instead.

**Pipeline:** (`overall` starts `not_started`)
1. Create `feature.yaml` from `AI/templates/feature.template.yaml` if missing — set
   `base_ref` to `git rev-parse HEAD` **now**, before any task touches code. This is the
   diff base for G3/G5; do not leave it as the template placeholder.
2. Run equivalent of `/architect-plan` → `plan.md` (reads `AI/project/prompts/architect.md`
   at this point — this is the only phase that needs it)
3. **STOP (Gate G0):** Ask user to confirm plan before continuing unless they said to proceed → on confirm set `overall: planned`
4. Run equivalent of `/split-tasks` → task files in `tasks/` → set `overall: tasks_split`
5. Create/update `status.yaml` from `AI/templates/status.template.yaml`
6. **Lint gate** — run `AI/scripts/lint-feature.ps1 -FeatureId <feature-id>` (or `/lint-feature`). If errors → **STOP** and fix before continuing.
7. Continue to **Implement loop** (sets `overall: implementing`)

### `--resume`

Use when the feature already has plan + tasks, or after a **change request**.

**Preflight checks:**

0. **Lint gate** — run `AI/scripts/lint-feature.ps1 -FeatureId <feature-id>` (or `/lint-feature`). If errors → **STOP** and fix before continuing.

1. **Change-request gate (CR gate)**
   - Read `spec.md` `## Changelog` and all `decisions/CR-*.md`
   - Read `status.yaml` → `last_processed_change`
   - If a CR is newer than `last_processed_change` → **STOP**, run change-request flow first

2. **Pending new tasks gate** — plan lists task but `tasks/TASK_NN.md` missing → **STOP**, run `/add-task`

3. **Blocked gate** — any task in `status.yaml` is `blocked` → **STOP**

**Pipeline:**
1. Load `status.yaml` (create from tasks/reviews if missing)
2. Find tasks with status `pending` or `in_progress`, in numeric order
3. Continue to **Implement loop**

---

## Implement loop

For each task in order:

### Skip rules
- Skip tasks with status `done` or `superseded`
- Skip `type: validation` tasks (manual QA in Unity)
- Leave validation tasks `pending`; remind user at Gate G4

On entering the loop, set `overall: implementing`.

### Code tasks

1. Set task `status: in_progress` in `status.yaml` (task `pending → in_progress`)
2. Run `/implement-task AI/features/<feature-id>/tasks/TASK_NN.md` — this reads
   `AI/project/prompts/developer.md` itself; do not pre-load it earlier in this run.
3. **Compile gate (G2)** — run `AI/scripts/compile-unity.ps1`. If compiler errors → task `→ blocked`, `overall: blocked`, **STOP**; fix and re-implement before review. If Unity unavailable or project locked in another editor → **STOP** unless user confirms clean compile; then rerun with `-SkipIfUnavailable`.
4. Run `/review-task AI/features/<feature-id>/tasks/TASK_NN.md` — this reads
   `AI/project/prompts/reviewer.md` itself; do not pre-load it earlier in this run. This is
   the **fast inline review** (in your own context) that keeps the loop moving. The
   **independent** cold review of the whole feature runs once at **Gate G3** below, not per
   task.
5. Record the review outcome on the task (`review:` = `passed` for a clean approve — **no file** — or a `reviews/REVIEW_TASK_NN.md` path when findings exist; see `review-task.md`):
   - **Approved** → task `done`
   - **Must fix** → task `blocked`, `overall: blocked`, **STOP (Gate G1)**
6. **Task-loop gate** — unless `--all` was passed, **STOP** here even on Approved: this run
   touches only one code task. Print the next command
   (`/build-feature <feature-id> --resume`, or add `--all` to run the rest without stopping)
   and recommend a **fresh chat session** before running it. With `--all`, continue to the
   next pending code task instead of stopping.

### After all pending code tasks are `done` — the automated review gate (G3 + G5)

Both **G3** and **G5 run automatically here, every time** — as **independent cold passes**, not inline
in your own context, and without waiting for the user to ask. Dispatch them together (they can run in
parallel as two background subagents); reconcile both before continuing.

0. **Compute the diff scope before dispatching either gate** — never let a subagent derive it
   itself:
   - Paths: the union of every `## Files allowed to touch` entry across this feature's
     `tasks/TASK_*.md`.
   - Base: `feature.yaml → base_ref`. If present → `git diff <base_ref>..HEAD -- <paths>`.
     If missing (older feature, backfilled without one) → `git diff HEAD -- <paths>`
     (working tree only) and say so in what you hand the subagent.
   - **Never** hand a subagent a bare `git diff`, `git diff main`, `git diff main...HEAD`,
     or any other branch comparison — `main` can be arbitrarily far behind and turn a
     34-file feature into a 20,000-file diff. Pass the exact `git diff` command, not a
     description of "the feature diff".
1. **Independent code review (Gate G3)** — dispatch the exact scope from step 0 to the
   independent `reviewer` subagent (Agent/Task tool → `reviewer` agent). Do **not** review it
   in your own context — the point is a cold, unbiased pass over the integrated change (best
   blast-radius and baseline analysis). Give it: the feature id, the exact `git diff` command
   (not a description), the spec, and the developers' declared risks; it reads the real code
   itself.
2. **Security review (Gate G5)** — dispatch at this same gate, **unless the feature has no security
   surface**: skip only when `AI/profile.yaml` shows no networking, no analytics, and
   `config_model.source: none` (or the diff does not touch config), and the diff has no
   network/file I/O — then record `G5: skipped — no security surface (see profile.yaml)` and move on.
   Otherwise dispatch it as an independent cold pass too (a subagent running
   `AI/commands/security-review.md` over the same exact scope from step 0 — same treatment as
   G3, so it is unbiased; Claude Code may also use `/security-review`). Do **not** fill the
   checklist inline in your own context, and do not skip it just because the diff "looks safe"
   — the skip condition above is the only valid reason.

**Reconcile both:**
- Any **Must-fix** (G3) or **Critical/High** (G5) → set the affected task(s) `blocked`,
  `overall: blocked`, **STOP**; fix via re-implement, then **re-run this gate** (both passes).
- G3 **pass** and G5 **pass or skipped** → record each outcome, set `overall: qa_pending`.
- _Cursor (no subagents): fall back to inline feature-level + security review, or ask the user to
  re-run them in a fresh session for independence._

3. If CR processed or acceptance changed → run `/test-feature <feature-id>`
4. **STOP (Gate G4):** manual QA; do not mark validation tasks `done` without user confirmation. When confirmed → validation tasks `→ done`, `overall: done`. Optionally remind the user they can run `/code-review` here too (early, whole-branch gut-check) — it is not required at this point, only before the final merge (see Rules below).

---

## After change request

```text
/change-request fishing-flow-ab-test
/update-plan fishing-flow-ab-test
/add-task fishing-flow-ab-test --task NN
/build-feature fishing-flow-ab-test --resume
```

Set `last_processed_change` in `status.yaml` when CR pipeline is complete.

## Rules

- **Before merging this feature's branch into `main`**, remind the user to run `/code-review`
  (Gate G6, manual — see `AI/core/state-machine.md`). G3 only reviewed this feature's declared
  file scope; `/code-review`'s default whole-branch diff also covers anything else accumulated
  on the branch (other merged features/fixes) that G3 never saw. Do not run it yourself — it is
  user-triggered only.
- Do **not** run full replan or re-split in `--resume`
- Do **not** renumber or overwrite done tasks
- Update `status.yaml` after every task state transition
- Pause at G0 and G4 unless user said to continue
- Pause after every code task (Task-loop gate) unless `--all` was passed

## After completing

Output: mode, feature id, `status.yaml` path, overall status, tasks completed, STOP reason, next
command. **Recommend starting a fresh chat session** before running that next command — a
session that stays open across tasks or features accumulates every prior task's diff, review,
and compile output, which compacts and gets re-read unnecessarily. This applies between tasks
(default `--one`) and between features; it does not apply mid-task.
