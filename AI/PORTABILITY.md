# Portability map — framework ↔ instance

This pipeline is designed to be reused across **any Unity project**. To port it,
you copy the **framework** parts unchanged and rewrite only the **instance** parts.
Unity itself is the constant; what varies between projects is captured in
[`profile.yaml`](profile.yaml).

> **Step 1 note:** this is a *logical* boundary. Files are **not** physically moved
> into an `AI/core/` folder yet, because commands are invoked through thin pointers
> in `.claude/commands/` and `.cursor/commands/` and a physical move would break
> those paths. Relocation is a later, separate step. For now, treat the table below
> as the contract for what is portable.

## Legend

- **framework** — Unity-generic. Copy as-is to a new project; do not edit per project.
- **instance** — project-specific. Lives in (or should be driven by) `profile.yaml`.
- **mixed** — currently blends both; a later step (de-duplication) will split it so
  the generic part becomes framework and the specific part reads from `profile.yaml`.

## Map

| Path | Class | Notes |
|------|-------|-------|
| `AI/core/rules/unity-core.md` | framework | **Canonical stack-neutral invariants.** The single source; everything else references it. (Added Step 2.) |
| `AI/core/state-machine.md` | framework | **Canonical pipeline states + legal transitions.** `status.yaml` and `build-feature.md` obey it. (Added Step 3.) |
| `AI/core/handoffs.md` | framework | **Canonical role handoff contracts** — what each role requires from upstream / guarantees downstream. Role prompts reference it. (Added Step 4.) |
| `AI/core/unity-failure-modes.md` | framework | **Canonical Unity/C# failure-mode catalog** — edge-case battery, footgun catalog, blast-radius list. Reviewer's edge-case battery & Tester's stress source. |
| `AI/commands/fix-bug.md` + pointers | framework | **Defect workflow** — feature-independent bug ledger; capture → classify origin → fix → gates → verify → close. |
| `AI/templates/bug.template.md` | framework | Defect record template. |
| `AI/bugs/index.yaml` | instance | This project's defect ledger. |
| `AI/core/evals/**` | framework | **Command eval kit** — methodology, frozen fixture, conformance checks, rubric, scorecard template. Run before/after editing a prompt. (Added Step 6.) |
| `AI/commands/*.md` | framework | Orchestration logic is generic. Concrete tool names should read from `profile.yaml`. |
| `AI/templates/*` | framework | Spec/task/status/feature scaffolds. |
| `AI/scripts/compile-unity.ps1` | framework | Generic Unity batchmode compile; entry point wired via `profile.yaml → gates.compile`. |
| `AI/scripts/lint-feature.ps1` | framework | Mechanical invariant checks. |
| `AI/README.md`, `AI/QUICKSTART.md` | framework | Workflow docs. |
| `AI/project/prompts/architect.md` | framework | De-hardcoded (Step 8): context anchors now read `profile.yaml` slots; no concrete tool names, project name from `profile.meta`. |
| `AI/commands/init-ai-pipeline.md` | framework | **Scaffold command** — bootstraps the pipeline into a new Unity project. (Added Step 7.) |
| `AI/templates/profile.template.yaml` | framework | Blank questionnaire the scaffold copies to `profile.yaml`. (Added Step 7.) |
| `AI/project/prompts/reviewer.md` | framework | De-hardcoded (Step 8): stack/QA-target/A-B-baseline/server-config lenses are now **conditional on `profile.yaml`**, not hardcoded FF facts. |
| `AI/project/prompts/tester.md` | framework | De-hardcoded (Step 8): DI/asset/WebGL required checks now gated on `profile.stack.*` / `profile.platforms`. |
| `AI/project/prompts/developer.md` | framework | De-hardcoded (Step 8): stack block reads `profile.yaml` slots; `none` slot ⇒ pattern N/A. Nothing instance-editable remains. |
| `AI/project/unity-rules.md` | framework | Generic thin pointer to `unity-core.md` + `profile.yaml` (Step 8: project name + storage note removed; no per-project edit needed). |
| `AI/project/context.md` | instance | Project overview, exact packages, composition root, folders. |
| `AI/profile.yaml` | instance | **The single questionnaire.** All per-project choices. |
| `AI/features/**` | instance | Actual feature work for this project. |
| `CLAUDE.md` (repo root) | instance | Always-on digest, regenerated per project (init Phase C): one stack line + pointers to `unity-core.md` and `profile.yaml`. |
| `.claude/skills/unity-csharp` | framework | Thin pointer to `developer.md` + `unity-rules.md` (already delegated; no copy). |
| `.claude/skills/reviewer,tester,grill` | mixed | Role knowledge; mostly generic. |
| `.claude/agents/reviewer.md` | framework | Read-only independent reviewer **subagent** for the Gate G3 feature review + risky bug fixes. **Claude Code only** — Cursor has no subagent equivalent (inline fallback). |
| `.claude/commands/*`, `.cursor/commands/*` | framework | Thin pointers to `AI/commands/`. |

## What "porting to a new Unity project" will look like (target state)

1. Copy the **framework** rows above into the new repo (no edits — verified generic as of Step 8).
2. Rewrite **`profile.yaml`** for the new project (DI container, reactive lib, UI
   framework, QA target, protected paths, legacy zones, gate scripts).
3. Regenerate `context.md` and the `CLAUDE.md` digest from the new project's actual stack.

That's the whole per-project surface: **`profile.yaml` + `context.md` + `CLAUDE.md`** (the
three `instance` rows). Everything else — prompts, commands, core, templates, scripts — is
framework and needs no edits, because it reads the concrete facts from `profile.yaml`.

## Next steps (from the improvement plan)

- **Step 2 — De-duplicate rules:** ✅ done. Invariants collapsed into
  `AI/core/rules/unity-core.md`; `CLAUDE.md`, `developer.md`, `reviewer.md`,
  `tester.md`, and `unity-rules.md` now reference it and pull stack names from
  `profile.yaml`.
- **Step 3 — State machine:** ✅ done. `AI/core/state-machine.md` defines feature/task
  states + legal transitions; `status.template.yaml` and `build-feature.md` obey it.
- **Step 5 — Review as gate-outcome:** ✅ done. `review-task.md` now records
  `review: passed` in `status.yaml` for clean approves (no file) and writes
  `REVIEW_TASK_NN.md` only when findings exist.
- **Step 4 — Role handoff contracts:** ✅ done. `AI/core/handoffs.md` defines the
  producer→artifact→consumer chain with per-seam contracts and a rejection rule;
  architect/developer/reviewer/tester prompts each carry an Input/Output block.
- **Step 6 — Evals:** ✅ done. `AI/core/evals/` provides the conformance + stability
  methodology, a frozen fixture spec, deterministic conformance checks, a worked
  `architect-plan` rubric, and a scorecard template.
- **Step 7 — Scaffold command:** ✅ done. `/init-ai-pipeline` bootstraps the framework
  into a new Unity project (detect stack → generate `profile.yaml` from template →
  regenerate `context.md` → write `CLAUDE.md` digest → verify gates). `architect.md`
  stack anchors de-hardcoded.
- **Step 8 — Finish prompt de-hardcoding:** ✅ done. The 4 role prompts
  (`architect/developer/reviewer/tester`) and `unity-rules.md` no longer name concrete
  tools (Zenject/UniTask/UniRx/Stepico/Addressables) or FF-only concepts (Classic baseline,
  server storage, "Fishing Fortune"): stack slots, QA-target constraints, A/B-baseline and
  server-config lenses are now **conditional on `profile.yaml`**. Surfaced while porting the
  pipeline to the **Threading Lab** project (`C:\Projects\ThreadingLab`) — the residual
  hardcoding would have mis-instructed roles there (e.g. "use Zenject", "no threads on WebGL").
  Per-project surface is now exactly `profile.yaml` + `context.md` + `CLAUDE.md`.

**Independent review (Gate G3):** per-task review stays inline/cheap; a cold read-only
`reviewer` subagent reviews the whole feature diff once before `qa_pending` (best blast-radius
+ baseline analysis, paid once). `/fix-bug` tiers by risk (trivial → inline, risky → subagent).
Claude Code only; Cursor falls back to inline. Definition: `.claude/agents/reviewer.md`.

**All 8 improvement steps complete.** The framework/instance split, canonical rule &
state-machine & handoff sources, gate-outcome reviews, the eval kit, the scaffold command,
and full prompt de-hardcoding are in place — a real second-project port (Threading Lab)
confirmed the per-project surface is just `profile.yaml` + `context.md` + `CLAUDE.md`.
Remaining optional polish: physically relocate framework files into `AI/core/` (updates
every command pointer), genericize the `fishing-flow-ab-test` usage examples in
`AI/commands/*` (cosmetic), and retro-clean the 15 legacy review files.
