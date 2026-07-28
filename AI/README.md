# AI Workflow — Fishing Fortune

Feature-centric layout: each feature is a self-contained folder under `AI/features/`.

**Onboarding:** [`QUICKSTART.md`](QUICKSTART.md)  
**Slash commands:** canonical bodies in `AI/commands/`; thin per-tool pointers in `.cursor/commands/` (Cursor) and `.claude/commands/` (Claude Code). Same `/<name>` + `$ARGUMENTS` contract in both. **Edit logic only in `AI/commands/`** — the pointers never need to change.

## Folder structure

```
AI/
├── commands/                # Canonical command bodies (tool-neutral, single source of truth)
├── project/                 # Global project rules + role prompts
│   ├── context.md
│   ├── architecture.md
│   ├── unity-rules.md
│   └── prompts/
├── features/
│   ├── index.yaml           # Registry of all features
│   └── <feature-id>/        # e.g. fishing-flow-ab-test
│       ├── feature.yaml
│       ├── status.yaml
│       ├── spec.md
│       ├── plan.md
│       ├── tasks/
│       ├── reviews/
│       ├── decisions/
│       └── qa/
├── templates/               # spec.template.md, task.template.md, …
└── archive/
```

## Context files (read by most commands)

- `AI/project/context.md`
- `AI/project/architecture.md`
- `AI/project/unity-rules.md`

## End-to-end workflow

**Orchestrated (recommended):**

```text
/build-feature fishing-flow-ab-test
/build-feature fishing-flow-ab-test --resume
```

**Manual:**

```text
1. Write spec     →  AI/features/<feature-id>/spec.md
2. Architect plan →  /architect-plan <feature-id>
3. Split tasks    →  /split-tasks <feature-id>
4. Per code task:
     /implement-task <feature-id>/tasks/TASK_NN.md
     /review-task <feature-id>/tasks/TASK_NN.md
5. Feature QA     →  /test-feature <feature-id>
6. Progress       →  AI/features/<feature-id>/status.yaml
```

## Bugs vs requirement changes

Two different things — do not confuse them:

- **Bug** = code fails to do what the spec/old behavior already promised → `/fix-bug`.
  Bugs live in one ledger `AI/bugs/` and may be **unrelated to any feature** (legacy code,
  a feature that broke old code, or an older defect). See lifecycle in
  [`core/state-machine.md`](core/state-machine.md).
- **Requirement change** = we now want *different* behavior → `/change-request` (below).

```text
/fix-bug "describe the defect"      # capture → classify origin → fix → G2 → review → verify → close
/fix-bug BUG-001                    # resume an existing bug
```

## Requirement changes

```text
/change-request <feature-id> + description
/update-plan <feature-id>
/add-task <feature-id> --task NN
/build-feature <feature-id> --resume
/test-feature <feature-id>    # if acceptance changed
```

Do **not** edit completed tasks; append new ones.  
Do **not** `/build-feature` without `--resume` on existing features.

## Slash commands

Available identically in Cursor and Claude Code. Each `.cursor/commands/<name>.md` and `.claude/commands/<name>.md` is a thin pointer to `AI/commands/<name>.md`; change behavior there.

**Knowledge auto-loads separately from commands:**
- **Always-on** project rules — `CLAUDE.md` (Claude Code) and `.cursor/rules/project.mdc` (Cursor). No need to `Read:` context/architecture/unity-rules in commands anymore.
- **On-demand skills** in `.claude/skills/` (read by both Cursor and Claude Code) — `unity-csharp`, `reviewer`, `tester`, `grill`. Point to canonical bodies in `AI/project/`. `grill` is now a **skill** (auto-triggers on "grill me on …"), not a slash command.

| Command | Example |
|---------|---------|
| `/init-ai-pipeline` | `/init-ai-pipeline` (bootstrap into a new Unity project) |
| `/compile-unity` | `/compile-unity` |
| `/lint-feature` | `/lint-feature fishing-flow-ab-test` |
| `/build-feature` | `/build-feature fishing-flow-ab-test` |
| `/build-feature --resume` | `/build-feature fishing-flow-ab-test --resume` |
| `/architect-plan` | `/architect-plan fishing-flow-ab-test` |
| `/split-tasks` | `/split-tasks fishing-flow-ab-test` |
| `/implement-task` | `/implement-task fishing-flow-ab-test/tasks/TASK_01.md` |
| `/review-task` | `/review-task fishing-flow-ab-test/tasks/TASK_01.md` |
| `/security-review` | `/security-review fishing-flow-ab-test` |
| `/test-feature` | `/test-feature fishing-flow-ab-test` |
| `/fix-bug` | `/fix-bug "sound toggle doesn't persist"` |
| `/change-request` | `/change-request fishing-flow-ab-test` |
| `/update-plan` | `/update-plan fishing-flow-ab-test` |
| `/add-task` | `/add-task fishing-flow-ab-test --task 13` |

## Porting to another Unity project

This pipeline is split into a stack-neutral **framework** and a per-project **instance**
(see [`PORTABILITY.md`](PORTABILITY.md)). To reuse it elsewhere:

1. Copy the framework: `AI/core/`, `AI/commands/`, `AI/templates/`, `AI/scripts/`,
   `AI/project/prompts/`, and the `.claude/` + `.cursor/` command pointers.
2. Run `/init-ai-pipeline` in the new repo — it detects the stack, generates
   `AI/profile.yaml` from the template, regenerates `context.md`, and writes the always-on
   `CLAUDE.md` digest.
3. Rewrite only `AI/profile.yaml` for anything that couldn't be auto-detected.

Canonical, stack-neutral pieces the framework relies on:
`AI/core/rules/unity-core.md` (invariants) · `AI/core/state-machine.md` (pipeline states) ·
`AI/core/handoffs.md` (role contracts) · `AI/core/evals/` (command evals).

## Naming

| Item | Path |
|------|------|
| Feature id | kebab-case slug, e.g. `fishing-flow-ab-test` |
| Code name | PascalCase in `feature.yaml`, e.g. `FishingFlowAbTest` |
| Task | `tasks/TASK_NN.md` |
| Review | `reviews/REVIEW_TASK_NN.md` |
| Change request | `decisions/CR-NNN-slug.md` |
| QA manual | `qa/manual.md` |
| Status | `status.yaml` |

## Spec traceability (REQ / AC)

New specs use `AI/templates/spec.template.md`:
- **REQ-001…** — functional requirements (what the system must do)
- **AC-001…** — acceptance criteria (Given / When / Then)

Tasks link back via optional block:

```markdown
## Traceability
- **requirements:** REQ-001, REQ-004
- **acceptance:** AC-002
```

`/test-feature` maps QA checks to **AC-*** IDs. **`/build-feature`** runs lint after tasks exist and **Unity compile (G2)** between implement and review. Scripts: `AI/scripts/lint-feature.ps1`, `AI/scripts/compile-unity.ps1`.

## Roles

| Role | Prompt |
|------|--------|
| Architect | `project/prompts/architect.md` |
| Developer | `project/prompts/developer.md` |
| Reviewer | `project/prompts/reviewer.md` |
| Tester | `project/prompts/tester.md` |

## Example: fishing-flow-ab-test

```text
/build-feature fishing-flow-ab-test
/implement-task fishing-flow-ab-test/tasks/TASK_01.md
/review-task fishing-flow-ab-test/tasks/TASK_01.md
/test-feature fishing-flow-ab-test
```

Status: `AI/features/fishing-flow-ab-test/status.yaml`
