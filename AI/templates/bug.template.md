# BUG-<NNN> — <short title>

<!-- One defect = one record. Lives in AI/bugs/, independent of any feature.
     Bug lifecycle + transitions are canonical in AI/core/state-machine.md. -->

## Status
reported            <!-- reported | fixing | in_review | verifying | closed | wont_fix | blocked -->

## Severity
<critical | high | medium | low>

## Origin
<feature-regression | feature-broke-legacy | pre-existing | unknown>
- **related_feature:** <feature-id, or none>
- **violated_ac:** <AC-0xx of that feature if it broke a spec'd behavior, or none>
- **suspected_cause_commit:** <sha / PR, if known, or none>

## Repro
- **Steps:** <1… 2… 3…>
- **Expected:** <what should happen — the correct behavior>
- **Actual:** <what happens instead>
- **Environment:** <editor | webgl | device | …>
- **Frequency:** <always | intermittent | once>

## Suspected area / root cause
- **Files / systems:** <paths or subsystems>
- **Hypothesis:** <why it happens — may be revised after tracing>

## Fix
- **Files allowed to touch:** <keep minimal>
- **Approach (minimal):** <smallest change that removes the defect>
- **Regression guard:** <the test or QA check that would catch this again — required>

## Verification
- [ ] Repro no longer reproduces
- [ ] Compiles clean (Gate G2)
- [ ] Reviewed (verdict recorded below)
- [ ] Regression guard in place
- [ ] Related AC re-validated (only if `violated_ac` is set)

## Review outcome
<review: passed — or a summary / link when there were findings>

## Resolution
<closed: what changed and where — filled when status → closed>
