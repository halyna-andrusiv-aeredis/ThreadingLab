# Role handoff contracts (canonical)

Single source of truth for what each role **requires from upstream** and **guarantees
downstream**. A handoff is valid only when the producer's outputs satisfy the contract;
the consumer may reject an incomplete handoff and STOP rather than guess. Stack-neutral —
concrete tool names come from [`AI/profile.yaml`](../profile.yaml).

## The chain

```
spec.md ──▶ ARCHITECT ──▶ plan.md ──▶ (split-tasks) ──▶ tasks/TASK_NN.md
   ──▶ DEVELOPER ──▶ git diff + deliverables ──▶ REVIEWER ──▶ verdict
   ──▶ (all code done) ──▶ TESTER ──▶ qa/manual.md ──▶ human manual QA (Gate G4)
```

## Handoff table

| # | Producer | Artifact | Consumer | Contract — the artifact MUST carry |
|---|----------|----------|----------|-------------------------------------|
| 1 | Spec author / change-request | `spec.md` | **Architect** | Numbered `REQ-*` requirements and `AC-*` acceptance criteria. Missing detail is allowed but must be resolvable by stated assumption. |
| 2 | **Architect** | `plan.md` + task breakdown | **Split-tasks / Developer** | Per task: Goal · Files affected · Type · Expected result · Validation/check · Rollback risk · Traceability (`REQ`/`AC`). Tasks are small, safe, independently reviewable. |
| 3 | **Split-tasks** | `tasks/TASK_NN.md` | **Developer** | Goal, Files-allowed-to-touch, Acceptance, Traceability. One focused outcome per task. |
| 4 | **Developer** | git diff + deliverables | **Reviewer** | Compiles clean (G2) · changed files · why each changed · summary · risks/edge cases · notes for reviewer. Diff stays within the task's Files-allowed-to-touch. |
| 5 | **Reviewer** | verdict | **Orchestrator / Developer** | `review: passed` (clean) **or** must-fix findings with minimal suggested fixes (task `→ blocked`). Recorded per `review-task.md` (Step 5). |
| 6 | **(all code tasks done)** | implemented feature | **Tester** | Every code task `done`; security (G5) passed. Tester receives the spec's `AC-*` set to map checks against. |
| 7 | **Tester** | `qa/manual.md` | **Human (Gate G4)** | Checks mapped to `AC-*` IDs; minimal, high-signal, touched-flow-focused. |

## Rejection rule

If an incoming artifact violates its contract (e.g. a task with no Files-allowed-to-touch,
a plan task with no traceability, a diff that edits files outside the task scope), the
consumer role **stops and reports the gap** instead of proceeding on assumptions. This is
what makes the pipeline composable: each stage trusts a known-shape input.

## Why explicit handoffs

Multi-agent pipelines fail at the seams, not inside a role. Naming the artifact and its
required shape at each seam is what lets `/build-feature` run roles back-to-back without a
human re-checking every transition — and lets any role be swapped or re-run in isolation.
