# AI Workflow — короткий огляд (Fishing Fortune)

Фіча = **одна папка** в `AI/features/<feature-id>/`. Команди: канон у `AI/commands/`, тонкі покажчики в `.cursor/commands/` (Cursor) і `.claude/commands/` (Claude Code) — однаковий `/<name>` + `$ARGUMENTS` в обох тулах. Правити логіку тільки в `AI/commands/`.

---

## 4 ролі

| Роль | Prompt | Робить |
|------|--------|--------|
| Architect | `project/prompts/architect.md` | Plan, tasks, CR — без коду |
| Developer | `project/prompts/developer.md` | Код однієї task |
| Reviewer | `project/prompts/reviewer.md` | Review diff |
| Tester | `project/prompts/tester.md` | QA checklist (`qa/manual.md`) |

---

## Структура

```
AI/features/fishing-flow-ab-test/
├── feature.yaml      # metadata
├── status.yaml       # прогрес tasks
├── spec.md           # WHAT
├── plan.md           # HOW
├── tasks/            # TASK_01…
├── reviews/
├── decisions/        # CR-001-…
└── qa/manual.md      # manual test checklist
```

Глобальні правила: `AI/project/` (context, architecture, unity-rules)

**Spec:** `REQ-*` (requirements) + `AC-*` (Given/When/Then). Tasks → `## Traceability`. Template: `AI/templates/spec.template.md`

---

## Grill me (до workflow)

**Grill me** — скіл `grill` (авто-тригер на «grill me on …», у Cursor і Claude Code). Stress-test плану **до** коду.

**Коли:** перед `spec.md`, перед `/architect-plan`, або перед великим change request — коли багато невизначеності в дизайні.

**Як викликати в чаті:**

```text
Grill me on AI/features/<feature-id>/spec.md
```

або

```text
Grill me on the popup flow before we write the spec
```

**Що робить:** ставить питання по одному, проходить гілки decision tree, пропонує recommended answer; може читати codebase. Мета — спільне розуміння **до** architect / build-feature.

**Не плутати з:** `/review-task` (код після implement) або `/change-request` (формалізація вже прийнятого рішення).

---

## Flow — нова фіча

```
spec.md → /build-feature <feature-id> → implement/review → /test-feature → QA в Unity
```

---

## Flow — change request

```
/change-request → /update-plan → /add-task → /build-feature --resume → /test-feature
```

---

## Команди (feature-id замість довгих шляхів)

```text
/build-feature fishing-flow-ab-test
/implement-task fishing-flow-ab-test/tasks/TASK_01.md
/test-feature fishing-flow-ab-test
```

---

## Stop gates

| Gate | Коли |
|------|------|
| G0 | Підтвердити plan |
| CR | Спочатку change-request flow |
| G1 | Review Must fix |
| G2 | Unity compile (batchmode, between implement and review) |
| G5 | Security review (Critical/High on feature diff, before manual QA) |
| G4 | Manual QA (validation tasks) |

**Lint:** вбудований в `/build-feature` — після split tasks (`--new`) і на старті `--resume`. Окремо `/lint-feature` — перед `overall: done`.

---

## Приклад

**fishing-flow-ab-test** — A/B fishing flow  
Статус: `AI/features/fishing-flow-ab-test/status.yaml`  
QA: `AI/features/fishing-flow-ab-test/qa/manual.md`

Деталі: [`README.md`](README.md)
