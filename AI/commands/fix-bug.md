# Fix Bug

Handle a defect end-to-end — one that may or may not belong to a pipeline feature. A bug is
**code failing to do the right thing**, not a requirement change (for requirement changes use
`/change-request`). Bugs live in a single ledger `AI/bugs/`, independent of features.

Read:
- _Project rules auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS` — a bug description, or an existing `BUG-NNN` id to resume.
- `AI/core/state-machine.md` — the **Bug lifecycle** states/transitions this command obeys.
- `AI/project/prompts/developer.md` (fix) and `AI/project/prompts/reviewer.md` (review).

## Arguments
```text
/fix-bug "sound toggle doesn't persist after relaunch"
/fix-bug BUG-001            # resume an existing bug
```
If `$ARGUMENTS` is empty, ask for the defect description.

## Paths
- Registry: `AI/bugs/index.yaml`
- Record: `AI/bugs/BUG-<NNN>-<slug>.md` (from `AI/templates/bug.template.md`)

---

## Step 1 — Capture (status: reported)
1. Allocate the next `BUG-NNN`; create the record from `bug.template.md`.
2. Fill **Repro** (steps / expected / actual / environment) and **Severity**. If repro is
   unclear, ask before proceeding — a bug you cannot reproduce cannot be verified fixed.
3. Register in `AI/bugs/index.yaml`.

## Step 2 — Classify origin (routing decision)
Determine **Origin** — it decides how the fix is tracked:

| Origin | What it means | How it is handled |
|--------|----------------|-------------------|
| `feature-regression` | a pipeline feature broke its **own** spec'd behavior | set `violated_ac`; **reopen that feature** (Step 3A) |
| `feature-broke-legacy` | a feature's change broke old/legacy code (no AC covers it) | fix as a **bug unit** (Step 3B); link `related_feature` for context |
| `pre-existing` | older/independent defect, no feature involved | fix as a **bug unit** (Step 3B) |
| `unknown` | origin not yet established | trace first (developer Pass-2 style); reclassify, then route |

To attribute a regression, inspect history around the suspect area (`git log`/blame) and set
`suspected_cause_commit` when found.

## Step 3A — Feature regression (belongs to a feature's traceability)
1. Reopen the feature: in its `status.yaml`, `overall: done|qa_pending → implementing`.
2. Append a fix task via `/add-task <feature-id> --task NN` with **Traceability → the
   violated AC**; keep Files-allowed-to-touch minimal.
3. Set the validation task that covers that AC back to `pending` (forces re-QA).
4. Run `/build-feature <feature-id> --resume` — the normal loop implements → G2 → review →
   G5 → re-QA the affected AC. Link that task from the bug record.
5. When the feature closes the loop, set the bug `status: closed`.

## Step 3B — Bug unit (no feature / legacy)
Run the same gates, tracked inside the bug record (no feature folder needed):
1. `status: fixing`. Implement the **minimal** fix per `developer.md`, within the record's
   Files-allowed-to-touch. Add the **regression guard** (a test, or a named QA check).
2. **Compile gate (G2)** — `AI/scripts/compile-unity.ps1`. Errors → fix before review.
3. `status: in_review`. Review the diff — **tier by risk**:
   - **Trivial** (a one-liner within the allowed files, no async/DI/subscription/asset/legacy
     or baseline impact) → fast **inline** review per `reviewer.md`.
   - **Risky** (touches async, DI, subscriptions, the asset system, legacy/shared code, or could
     affect a control/baseline path) → dispatch to the **independent `reviewer` subagent**
     (Agent/Task tool → `reviewer` agent) for a cold pass. _Cursor: inline fallback._
   Record the outcome in the record. Must-fix → back to `fixing` (re-implement), then
   re-compile + re-review.
4. `status: verifying`. Re-run the repro steps; confirm it no longer reproduces and the
   regression guard is in place. This is the bug's Gate-G4 equivalent — needs user
   confirmation for anything not covered by an automated guard.

## Step 4 — Close (status: closed)
- Fill **Resolution** (what changed, where) and check the Verification boxes.
- Update `index.yaml`. If a fix is declined, use `wont_fix` with a one-line reason.

## Guardrails
- One defect per record; do not bundle unrelated fixes.
- Fix the **root cause**, not the symptom; keep the diff minimal (developer guardrails apply).
- A legacy fix still respects `profile.code_layout.legacy_zones` — minimal, no drive-by refactor.
- Never mark a bug `closed` without a reproduced-then-gone check (or explicit user sign-off).

## After completing
Output: bug id, origin, route taken (3A/3B), current status, gate results, and — if closed —
the resolution and regression guard.
