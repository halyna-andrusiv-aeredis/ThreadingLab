# Unity / C# failure modes (canonical catalog)

The non-obvious ways Unity/C# changes break in production — the difference between a
syntax check and a senior review. Used as the **edge-case battery** by the Reviewer and
the **stress-check source** by the Tester. Stack-neutral: concrete tools appear as
examples in parentheses; resolve them via [`profile.yaml`](../profile.yaml).

Not every item applies to every diff. Run a change through the relevant rows and keep the
ones with a **plausible, concrete** failure path — no speculative flagging.

## A. Edge-case battery (run every change through these)

**Inputs & data**
- null / empty / missing; zero / negative / max / overflow; malformed or out-of-range.
- Culture: `float`/`decimal` parse & `ToString` (server sends invariant culture — a comma
  locale silently corrupts numbers). Compare floats with epsilon, not `==`.
- Very large / empty collections; duplicate keys; unsorted assumed sorted.

**Timing & lifecycle**
- **Double invocation** (double-tap, repeated signal) — is the action idempotent, or does
  it spend the resource / fire the event twice?
- **Re-entrancy** — a handler that triggers the same handler again.
- **Rapid open/close** — screen closed before its async load/init finishes.
- **Cancellation mid-flight** — cancel during `await`; are partial effects rolled back?
- **await-after-destroy** — continuation resumes after the GameObject/View is gone →
  `MissingReferenceException` or acting on a dead object.
- **Scene reload / transition** — surviving systems (`DontDestroyOnLoad`) re-initialize or
  double-subscribe; late continuations land in a new scene.
- **Domain reload / Enter-Play-Mode settings** — static/singleton state not reset carries
  stale values between play sessions.
- **App pause / focus loss / backgrounding** — `timeScale == 0`, `OnApplicationPause`,
  audio suspend; timers using scaled vs unscaled time.
- **First launch vs returning user** — no stored value yet; migration of old saved data.

**External / IO**
- Network: failure, timeout, slow link, retry, **out-of-order responses**, response after
  the requester is gone.
- **Server-driven config missing / malformed / out-of-range** — does it fall back safely
  (e.g. to the baseline flow) instead of crashing or randomizing? Does mid-session config
  change get handled?
- Save/load failure, partial write, concurrent saves.

**Concurrency**
- Multiple concurrent runs of the same async op; which completion wins.
- Shared mutable state touched from overlapping flows.

## B. Unity/C# footgun catalog

**Lifecycle & timing**
- **DI injection timing** — with the DI container, injection runs *before* `Awake`; do not
  use injected fields in field initializers or constructors of MonoBehaviours.
- `Awake` / `OnEnable` / `Start` ordering and script-execution-order dependence.
- **Pooled objects** — `OnEnable`/`OnDisable` must pair re-subscribe/unsubscribe and reset
  state; a pooled object is reused, not recreated.
- **Coroutines stop on disable**; async flows do **not** — they keep running after the
  object is disabled/destroyed unless cancelled on lifetime.
- **Unity fake-null** — a destroyed `UnityEngine.Object` is `== null` true but is not a real
  null, so `?.` / null-coalescing can behave unexpectedly on Unity objects.

**Async**
- Fire-and-forget (`.Forget()` / `async void`) **swallowing exceptions** → silent failure.
- Completion source (e.g. `UniTaskCompletionSource`) never completed → permanent hang.
- `CancellationToken` not threaded through, or cancel not honored on teardown.
- `await` in a loop serializing calls that should be parallel (or the reverse).

**Reactive**
- `Subscribe` without a lifetime binding (`AddTo`/`Dispose`) → leak; re-subscribing on each
  screen open → **handlers multiply** (duplicate reactions).
- Reactive properties: emission on equal values unless distinct-until-changed; surprise
  initial emission on subscribe.
- Subjects not completed/disposed; per-frame operator chains allocating.

**Serialization & assets**
- Renaming a `[SerializeField]` field **loses inspector data** — needs `FormerlySerializedAs`.
- Changing field types or a ScriptableObject schema breaks existing assets.
- Prefab / variant / nested-prefab override loss; serialized reference rewiring.
- **Asset system (Addressables)** — load handle not released, or released twice; load race
  with scene unload; key/label change breaks addressing; `Instantiate` vs `InstantiateAsync`
  release discipline mismatch.

**DI container**
- Wrong binding scope (Transient where Single is needed → multiple instances, duplicated
  subscriptions; or Single where per-scope is needed → stale shared state).
- Missing binding → **runtime** resolve exception, not a compile error.
- Circular dependency; `IInitializable`/`IDisposable`/`ITickable` not registered so its
  lifecycle never runs; injecting into objects the container did not create.

**WebGL / IL2CPP (when primary QA target = webgl)**
- **Managed code stripping** removes reflection-only types → needs `[Preserve]` / `link.xml`.
- No threading; `Task.Run`, `Thread`, blocking waits (`.Result`/`.Wait()`) freeze or fail.
- `DateTime` timezone/precision; `System.Timers`; PlayerPrefs size & async flush to IndexedDB.
- Memory growth on repeated flows; wrap JS interop in try/catch.

**Logic & numeric**
- Enum default is `0` — does that mean a valid state? Adding enum values silently breaks
  exhaustive `switch`.
- Integer overflow, division by zero, off-by-one on boundaries.
- Time: `deltaTime` vs `unscaledDeltaTime` under pause/timescale.

## C. Blast radius (look beyond the diff)

- **Consumers** — who calls the changed class/method/interface? Are all call sites updated
  and still correct?
- **DI graph ripple** — a binding/scope change affects every injectee, not just this feature.
- **Serialized/interface change** — ripples to prefabs, ScriptableObjects, and other
  implementers not shown in the diff.
- **Shared / static / singleton state** touched → other features reading it.
- **Baseline preservation** — for an A/B test or refactor, does the control/baseline path
  stay behaviorally identical? A change that only *should* affect the variant must be proven
  not to touch the control.
- **Init order / global sequence** dependence introduced.
- **Analytics / event contracts** changed → downstream dashboards and funnels break silently.
