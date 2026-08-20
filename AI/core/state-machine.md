# Pipeline state machine (canonical)

Single source of truth for the states a feature and its tasks may hold, and the
**only** transitions allowed between them. `status.yaml` must always carry a value
from these enums; `/build-feature --resume` decides what to do next **from the state**,
not from ad-hoc heuristics. Stack-neutral — applies to any project using this pipeline.

## Feature-level state — `overall`

| State          | Meaning                                                        | How it is entered                          |
|----------------|----------------------------------------------------------------|--------------------------------------------|
| `not_started`  | Feature registered; no plan yet.                               | initial                                    |
| `planned`      | `plan.md` exists and was confirmed at Gate G0.                 | architect-plan + G0                        |
| `tasks_split`  | Task files exist under `tasks/`.                               | split-tasks                                |
| `implementing` | Code tasks are being implemented/reviewed (per-task loop).     | first code task starts                     |
| `qa_pending`   | All code tasks `done`, security (G5) passed; validation tasks await manual QA. | last code task done + G5 pass |
| `done`         | Every task `done`, including validation.                       | manual QA confirmed at G4                  |
| `blocked`      | A gate stopped the flow or a task is `blocked`.                | G1 must-fix / G2 compile fail / any blocker |

### Allowed feature transitions

```mermaid
stateDiagram-v2
    [*] --> not_started
    not_started --> planned: architect-plan + G0
    planned --> tasks_split: split-tasks
    tasks_split --> implementing: first code task starts
    implementing --> blocked: G1 must-fix / G2 compile fail / blocker
    blocked --> implementing: fix applied
    implementing --> qa_pending: all code done + G5 pass
    qa_pending --> done: manual QA confirmed (G4)
    qa_pending --> implementing: change request OR feature-regression bug
    done --> implementing: change request OR feature-regression bug
```

Any transition **not** listed is illegal. A `qa_pending`/`done` feature moves backward into
`implementing` only via a **processed change request** (CR gate in `build-feature.md`) **or a
feature-regression bug** (`/fix-bug` Step 3A); either may require re-entering `tasks_split` if
new tasks are added. Legacy/pre-existing bugs do **not** touch feature state — they run their
own lifecycle below.

## Task-level state — `status`

| State         | Meaning                                                     |
|---------------|-------------------------------------------------------------|
| `pending`     | Not started.                                                |
| `in_progress` | Being implemented.                                          |
| `blocked`     | Review returned **Must fix**, or compile (G2) failed.       |
| `done`        | Implemented + review approved (code); or confirmed (validation). |
| `superseded`  | Replaced by a later task / change request.                  |

### Allowed task transitions

```mermaid
stateDiagram-v2
    [*] --> pending
    pending --> in_progress: implement starts
    in_progress --> done: review approved
    in_progress --> blocked: must-fix / compile fail (G1/G2)
    blocked --> in_progress: re-implement
    pending --> superseded: change request replaces it
    done --> superseded: change request replaces it
```

**Validation tasks** (`type: validation`) are special: they may only go
`pending → done` via **manual QA confirmation at Gate G4** — never auto-marked by the
implement loop.

## Bug lifecycle — `AI/bugs/BUG-NNN` `status` (driven by `/fix-bug`)

A defect is its own unit, independent of features. It reuses the same gates (G2 compile,
review, verify) but has its own states.

| State       | Meaning                                                            |
|-------------|-------------------------------------------------------------------|
| `reported`  | Captured with repro + severity; origin not yet fixed.             |
| `fixing`    | Minimal fix being implemented (developer guardrails apply).       |
| `in_review` | Fix under review; Must-fix sends it back to `fixing`.             |
| `verifying` | Repro re-run; confirming it no longer reproduces + guard in place.|
| `closed`    | Fixed, verified, regression guard added.                          |
| `wont_fix`  | Deliberately not fixed (record the reason).                       |
| `blocked`   | Cannot proceed (needs info / decision / upstream fix).            |

```mermaid
stateDiagram-v2
    [*] --> reported
    reported --> fixing: origin classified (bug unit, Step 3B)
    fixing --> in_review: fix done + G2 pass
    in_review --> fixing: Must fix
    in_review --> verifying: review passed
    verifying --> fixing: still reproduces
    verifying --> closed: repro gone + guard in place
    reported --> wont_fix: declined
    reported --> blocked: needs info/decision
    blocked --> fixing: unblocked
```

**Feature-regression bugs** (Step 3A) instead reopen the owning feature
(`done`/`qa_pending → implementing`) and ride the feature state machine via an appended
fix task; the bug record tracks `reported → … → closed` alongside, closing when the feature
re-QA passes.

## Gate → transition map (how the orchestrator drives state)

| Gate | Fires when | Effect on state |
|------|------------|-----------------|
| G0   | plan produced             | `not_started → planned` (after user confirm) |
| —    | tasks written             | `planned → tasks_split`                       |
| —    | first code task starts    | `tasks_split → implementing`; task `pending → in_progress` |
| G2   | compile fails             | task `in_progress → blocked`; feature `→ blocked` |
| G1   | review = Must fix         | task `in_progress → blocked`; feature `→ blocked` |
| —    | review = Approved (inline, per task) | task `in_progress → done`          |
| G3   | all code done → **independent** review of the whole feature diff (cold `reviewer` subagent, or a 3-reviewer panel — see below) | Must-fix → task(s) `→ blocked`, feature `→ blocked`; else continue |
| G5   | G3 passed, security ok    | feature `implementing → qa_pending`          |
| G4   | manual QA confirmed       | validation tasks `pending → done`; feature `qa_pending → done` |
| CR   | change request processed  | feature `qa_pending`/`done` → `implementing` (+ `tasks_split` if new tasks) |

## Gate G3 — panel mode for high-risk features

A single cold `reviewer` pass has one reviewer's blind spots. For most features that's an
acceptable trade against speed/cost — but for changes with wide blast radius or high cost of
a missed bug (payments/IAP, save-data/migrations, auth), one reviewer's miss is expensive
enough to warrant redundancy.

**Trigger:** `feature.yaml → risk: high`, set at Gate G0 (architect proposes, user confirms —
see `build-feature.md`). Default is `risk: standard` (single reviewer, unchanged behavior).

**Panel mechanics** (`risk: high` only): dispatch 3 independent cold `reviewer` subagents on
the identical diff scope, in parallel, blind to each other (adversarial-verify pattern —
redundant independent judgment, not a single point of failure).

- **Must-fix confirmed by ≥2 of 3** → blocking, same as a single-reviewer Must-fix.
- **Must-fix raised by only 1 of 3** → not auto-blocking, but never silently dropped: recorded
  as a minority finding and put to the user for a manual call before the feature moves to
  `qa_pending`. The point of the panel is to catch what one reviewer misses, not to let two
  reviewers outvote a real bug a third one found.
- **Should-fix / Nice-to-have** — union of all three, deduplicated; no consensus needed.

This does not add a new state to the tables above — `risk` only changes *how* Gate G3 is
executed (1 reviewer vs. 3), not what `overall`/`status` values exist or how they transition.

## Gate G6 — pre-merge branch review (manual, not a feature-state gate)

G3 only ever sees the diff scoped to one feature's declared file list (or one bug's fix) —
by design, to avoid handing a reviewer a branch-wide diff. That scoping creates a blind spot:
a regression in a file **outside** the current task's scope, or in a previously-merged
feature still sitting on the same branch, is invisible to G3.

**G6 closes that gap**: before merging a feature/bug branch into `main`, run `/code-review`
(plain, or `ultra` for a heavier multi-agent pass on a large/long-lived branch) with no
scope argument — it reviews the whole branch diff against upstream by default, not just
one feature's file list.

- **Not automated** — `/code-review` is a user-invoked command (billed in `ultra` mode); no
  orchestrator dispatches it, and it does not appear in the Gate → transition map above.
- **Not tied to feature `overall`** — a branch can carry several `done` features/bugs; G6
  runs once per branch, at merge time, not once per feature.
- Findings are advisory: fix Must-fix-equivalent findings before merging, same bar as G3.

## Invariants (lint may enforce later)

- `overall` is always one of the feature-level states above.
- Every task `status` is one of the task-level states above.
- Feature is `qa_pending` **only if** no code task is `pending`/`in_progress`/`blocked`.
- Feature is `done` **only if** every task is `done` or `superseded`.
- Feature is `blocked` **iff** at least one task is `blocked` (or a gate recorded a blocker).
