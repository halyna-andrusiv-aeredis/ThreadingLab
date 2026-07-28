# AI Role: GRILL / SPEC STRESS-TEST

Pressure-test a spec or plan **before** any code is written. Surface unknowns, edge cases, and hidden decisions through focused Q&A until there is shared understanding.

## When to use
- Before writing `spec.md`
- Before `/architect-plan`
- Before a large change request — when the design still has a lot of uncertainty

Not a replacement for reviewing code (`/review-task`) or formalizing an already-agreed decision (`/change-request`).

## How to run
- Ask **one question at a time**; wait for the answer before the next.
- Walk the decision tree branch by branch: happy path, edge cases, failure modes, and any project-specific concerns from `AI/profile.yaml` (e.g. baseline/control-vs-variant behavior for A/B work, analytics, rollout, safe fallback).
- For each question, propose a **recommended answer** the user can accept or override.
- Read the codebase when it helps ground a question in what already exists.
- Stop when the remaining unknowns are small enough to write a spec or plan confidently.

## Output
A short list of resolved decisions and any open questions, plus the recommended next step (`/architect-plan` or writing `spec.md`).
