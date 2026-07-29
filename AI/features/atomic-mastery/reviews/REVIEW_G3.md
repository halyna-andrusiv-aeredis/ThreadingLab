# Gate G3 — Independent feature review: atomic-mastery

Reviewer: cold independent `reviewer` subagent (whole feature diff — new `AtomicMasteryScenario.cs` +
one registration line in `ThreadingLabHost.cs`).

## Pass 1 — Approved (no Must-fix; 3 Should-fix, 1 Nit)

Verdict: **Approved.** Functionally correct and thread-safe on the x64 Editor/Standalone QA target — no
leak, deadlock, or lost-update bug under normal use. Three Should-fix and one Nit recommended before merge.

Confirmed clean by the reviewer:
- No Unity API in worker bodies (only `Interlocked`/`Volatile`/locals/token/stack).
- `StructLayout` false-sharing structs: blittable `long` only, non-overlapping offsets (0/8, 0/128),
  no reference field under explicit layout, 8-byte aligned; `Interlocked.Increment(ref h.C.A)` interior
  managed pointer is valid and GC-safe.
- Treiber stack Push/TryPop CAS + fresh-`new` nodes → documented ABA mitigation holds; `CountRemaining`
  walked only post-join; conservation `pushed == popped + remaining` structurally guaranteed.
- Mode A: `final == expected` invariant for all three strategies; `Interlocked.Read`/`Exchange` on the
  64-bit counter.
- Lifecycle: main thread joins only the bounded runner; unbounded worker joins run off-main; linked-CTS
  `CancelAfter` + `StopRun().Cancel()` trip the polling loops; `Exit`/`OnDestroy`/mode-switch funnel
  through `StopRun`; no cross-thread Unity callback → no dispatcher deadlock.
- IMGUI styles cached once; `GuiEnabled` RAII balanced.

### Should-fix (all applied, see below)
- **S1 — Publication order inverted (consumer).** Draw methods read the result data *before* the volatile
  `_isRunning` acquire read; against the runner's release (`_isRunning = false` after writing results),
  the acquire must come first. Worst case: a torn `StackResult` renders a bogus "VIOLATED" conservation
  line for one frame in the very scenario meant to prove atomic correctness.
- **S2 — Shared `_workers` field + join-timeout orphan window.** `_workers` was an instance field mutated
  by the runner thread; if `_runner.Join(2000)` ever timed out, `StopRun` nulled `_runner`/`_isRunning`
  while the orphan lived, and a next `Run()` could start a second runner mutating the same list → list
  corruption + a leaked runaway worker (the exact failure REQ-013/AC-006 forbid).
- **S3 — IMGUI state mutated mid-OnGUI.** Mode buttons (change `_mode`) and Run (flip `_isRunning`) altered
  layout-driving state during the mouse-event pass, so the control count diverged from the cached Layout
  pass → intermittent `ArgumentException`/"Mismatched LayoutGroup" spam on the two most common clicks
  (cuts against AC-007's "no error in the Console").

### Nit
- **N1** — `Debug.LogWarning` on the runner thread is the file's single off-main Unity call; `Debug.Log*`
  is thread-safe, but it deserves a comment so a future reader neither "fixes" it nor copies a
  non-thread-safe call beside it.

## Fixes applied
- **S1:** `DrawGUI` snapshots `bool running = _isRunning;` (volatile acquire) once at the top of the pass,
  sequenced before any result read; Draw methods take `running` and read the flag-before-data.
- **S2:** `RunTimedWorkers` now keeps the worker array and per-thread op counts entirely **local** — the
  `_workers` field is removed, eliminating the shared-state footgun regardless of the timeout branch.
- **S3:** all layout-driving state is snapshotted at the top of `DrawGUI` and every button records a
  *pending* action (`pendingMode` / `pendingRun` / `pendingReset`) applied **after** `GUILayout.EndArea()`
  — never mid-pass — so the Layout and Repaint passes always emit the same controls.
- **N1:** comment added at the runner's `Debug.LogWarning` noting the deliberate thread-safe exception.

G2 PASS after fixes.

## Resolution
G3 approved (no Must-fix; recommended Should-fix S1–S3 + N1 all applied and re-compiled). Proceed to G4.
