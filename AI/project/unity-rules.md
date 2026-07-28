# Tech Constraints (per-project reference)

Short reference for AI roles. This file does not restate engineering rules or project
facts — both live in one place each. Read those, do not duplicate them here.

## Where the rules and facts live
- **Engineering invariants** (guardrails, DI, subscriptions, async, asset system,
  QA-target constraints, performance, lifecycle, quality bar): see
  **[`AI/core/rules/unity-core.md`](../core/rules/unity-core.md)** — canonical, stack-neutral.
- **Concrete stack & project facts** (Unity version, DI / async / reactive / UI / assets,
  analytics, networking, config model & safe fallback, protected paths, legacy zones,
  primary QA target): see **[`AI/profile.yaml`](../profile.yaml)** — the single questionnaire.
- **Full project overview & architecture**: see [`context.md`](context.md).

The neutral-term → `profile.yaml` mapping is in `unity-core.md`. If a fact is true for THIS
project but not for "any Unity project", it belongs in `profile.yaml`, not here.
