# Init AI Pipeline

Bootstrap this spec-driven pipeline into a Unity project — copy the **framework**,
generate the **instance** layer, and verify the gates. Run once per new project.

Read:
- `AI/PORTABILITY.md` — the framework ↔ instance boundary this command enforces.
- `AI/templates/profile.template.yaml` — the questionnaire to fill.
- `$ARGUMENTS` — optional target path (default: current repo root).

## Preconditions
- Target is a Unity project (has `Assets/`, `ProjectSettings/`, `Packages/manifest.json`).
- If `AI/profile.yaml` already exists → **STOP**: the pipeline is already initialized.
  Suggest editing `profile.yaml` directly instead of re-init.

## Phase A — Framework files (copy, do not edit)
Ensure these **framework** paths are present in the target (copy from the source repo if
missing). They are project-neutral and must not be hand-edited per project:
- `AI/core/**` (rules, state-machine, handoffs, evals)
- `AI/commands/**`, `AI/templates/**`, `AI/scripts/**`
- `AI/project/prompts/**` (role skeletons), `AI/README.md`, `AI/QUICKSTART.md`, `AI/PORTABILITY.md`
- `.claude/commands/**`, `.cursor/commands/**`, `.claude/skills/**`

If any are missing, list them and copy them before continuing.

## Phase B — Generate `AI/profile.yaml` (instance)
1. Copy `AI/templates/profile.template.yaml` → `AI/profile.yaml`.
2. **Detect** what you can from the repo instead of asking:
   - `engine.unity_version` ← `ProjectSettings/ProjectVersion.txt`.
   - `stack.*` ← scan `Packages/manifest.json` for known packages (DI, async, reactive,
     UI, addressables, analytics, networking, save). Map package → slot.
   - `code_layout` / `protected_paths` ← inspect top-level `Assets/` folders.
   - `platforms` ← enabled build targets / `ProjectSettings`.
3. **Ask the user only for what cannot be detected** (composition-root class names,
   `config_model`, legacy zones, primary QA target, known risks). One question at a time.
4. Fill every `<placeholder>`. Leave no angle brackets in the final file.

## Phase C — Generate instance docs
- `AI/project/context.md` — regenerate from the detected stack + architecture (overview,
  packages, composition root, folders). Do not copy the source project's `context.md`.
- `CLAUDE.md` (repo root) — write the always-on digest: one stack line + pointers to
  `AI/core/rules/unity-core.md` (canonical rules) and `AI/profile.yaml` (stack facts).
  Keep it a **digest**, not a second copy of the rules.
- `AI/features/index.yaml` — create empty (`features: []`).

## Phase D — Verify gates
- Confirm `AI/scripts/compile-unity.ps1` and `AI/scripts/lint-feature.ps1` resolve the
  Unity path / project for this machine; note any wiring the user must fix.
- Do **not** run a full compile here unless the user asks.

## Phase E — Smoke check (optional)
- Offer to dry-run `/architect-plan` against `AI/core/evals/fixtures/sample-spec.md` and
  grade it with `rubrics/architect-plan.md` — confirms the prompts hold under the new
  `profile.yaml`. Do not implement the fixture.

## After completing
Output: files created, values detected vs asked, anything the user must finish
(gate wiring, unresolved placeholders), and the suggested first command
(`/build-feature <feature-id> --new`).
