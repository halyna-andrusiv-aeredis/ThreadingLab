# Rubric — `/architect-plan`

Worked example. Copy its shape for other commands. Input: `fixtures/sample-spec.md`.
Run N = 3 fresh times; grade each run PASS / WEAK / FAIL, then judge stability across runs.

## Conformance criteria (per run — deterministic)
Apply the **Plan** section of [`../conformance-checks.md`](../conformance-checks.md):
1. Every task has Goal · Files · Type · Expected result · Validation · Rollback · Traceability.
2. Every spec `REQ-*` / `AC-*` is covered by ≥1 task.
3. No task edits `profile.protected_paths` without calling it out as a risk.
4. Output includes the Architect's required blocks: Architecture proposal · Impact analysis ·
   Files to create/change · Risks · Step-by-step plan.

Any conformance FAIL = the command is not eval-green; fix the prompt.

## Quality criteria (per run — judgement)
5. **Right-sized tasks.** Tasks are small and independently reviewable; no single task
   spans the whole feature. (For this fixture: expect ~2–4 tasks.) WEAK if one giant task
   or absurd over-splitting (>6).
6. **Reuse first.** Plan proposes reusing the project's existing save + audio services
   (per the fixture notes) rather than inventing a new system. FAIL if it invents a new
   audio/save framework.
7. **Assumptions stated, not blocking.** Missing detail handled by an explicit assumption.
8. **No stack contradictions.** Nothing conflicts with `profile.yaml` (DI, save, async).

## Stability criteria (across the N runs)
9. **Task count agrees within ±1** across runs.
10. **Files-to-touch agree** on the core files (the audio service + settings view/VM +
    save key) — naming may vary, targets should not.
11. **Same architectural approach** (extend existing services via DI) — no run should
    diverge into a fundamentally different design.
12. **Zero conformance FAILs** in any run.

## Verdict
- **Eval-green:** criteria 1–4 PASS in all runs; 5–8 PASS/WEAK (no FAIL); 9–12 hold.
- **Not green:** any conformance FAIL, or stability criteria break → revise the
  `architect-plan.md` / `architect.md` prompt and re-run.
