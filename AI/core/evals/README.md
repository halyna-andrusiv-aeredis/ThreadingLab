# Command evals

The commands in this pipeline are **prompts**, and prompts drift: a small wording change
can quietly make a command produce weaker or inconsistent output. Evals are how you catch
that before it reaches real feature work. Stack-neutral — part of the framework.

Run an eval **before and after editing a command prompt or a role prompt**, and when
porting the pipeline to a new project (to confirm the prompts still hold with a different
`profile.yaml`).

## Two kinds of eval

### 1. Conformance eval (deterministic)
Does one run's output satisfy the **handoff contract** for that command? These checks are
mechanical — a human or a script can grade them yes/no. They come straight from
[`../handoffs.md`](../handoffs.md) and [`../state-machine.md`](../state-machine.md).
Example: "every task in `plan.md` has Goal · Files · Type · Validation · Rollback ·
Traceability." Conformance is pass/fail; any failure is a bug in the command prompt.

### 2. Stability eval (variance)
Run the **same command on the same frozen fixture N times** (N = 3 is enough to feel drift)
and compare the structured outputs. You are not checking that runs are byte-identical —
they won't be — but that they agree on the **decisions that matter**: same task count ±1,
same files touched, same architectural approach, no contract violations in any run.
High variance on decisions = the prompt is under-specified.

## How to run

1. Freeze the input: use a fixture under [`fixtures/`](fixtures/) — never a live feature.
2. Run the command under test against the fixture (fresh context each run for stability).
3. Grade each run against the command's rubric in [`rubrics/`](rubrics/).
4. Record the run in a copy of [`scorecard.template.md`](scorecard.template.md) under
   `results/`.

Automation path: the `skill-creator` skill (Anthropic skills) can run repeated-trial evals
and variance analysis — wire a rubric into it once the manual protocol here is stable.

## Scoring scale (per rubric criterion)

| Score | Meaning |
|-------|---------|
| **PASS** | Criterion fully met. |
| **WEAK** | Met but sloppy / needs a human nudge (e.g. vague validation step). |
| **FAIL** | Contract violated. Blocks the command from being considered stable. |

A command is **eval-green** when: conformance = all PASS across all N runs, and stability =
no FAIL and decisions agree within tolerance. Any FAIL → fix the prompt, re-run.

## What exists here

- `fixtures/` — frozen inputs (a sample spec with REQ/AC).
- `rubrics/` — one rubric per command under test. `architect-plan.md` is the worked example.
- `scorecard.template.md` — copy per run into `results/`.
- `conformance-checks.md` — the deterministic checks shared across commands (candidate for
  scripting into `lint-feature.ps1` later).
