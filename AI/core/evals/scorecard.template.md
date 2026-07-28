# Eval scorecard — <command> — <YYYY-MM-DD>

- **Command under test:** /<command>
- **Fixture:** fixtures/<file>
- **Rubric:** rubrics/<file>
- **Prompt version / commit:** <git sha or note>
- **Runs (N):** 3

## Per-run conformance + quality

| Criterion | Run 1 | Run 2 | Run 3 |
|-----------|-------|-------|-------|
| 1 task fields complete | | | |
| 2 REQ/AC coverage | | | |
| 3 protected paths | | | |
| 4 required output blocks | | | |
| 5 right-sized tasks | | | |
| 6 reuse first | | | |
| 7 assumptions stated | | | |
| 8 no stack contradiction | | | |

(Scores: PASS / WEAK / FAIL)

## Stability (across runs)

| Criterion | Result | Notes |
|-----------|--------|-------|
| 9 task count ±1 | | e.g. 3 / 3 / 4 |
| 10 core files agree | | |
| 11 same approach | | |
| 12 zero conformance FAILs | | |

## Verdict
- [ ] **Eval-green** — safe to keep the prompt.
- [ ] **Not green** — action: <what to change in which prompt> → re-run.

## Observations
<Drift patterns, weak wording spotted, prompt lines to tighten.>
