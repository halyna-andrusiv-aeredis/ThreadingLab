# Gate G3 — Independent feature review: main-thread-freeze

Reviewer: cold independent `reviewer` subagent. Verdict: **Not approved → fixed → clean**.

## Verified (declared risks held up)
- Freeze duration varies by CPU — cosmetic demo trade-off, deterministic workload (REQ-006). OK.
- Main-thread min-FPS derived as 1000/ms (one blocked frame) — intended, not a bug.
- Background Task not joined; `_runId` generation guard — **no leak, no data race**: the worker
  touches only locals; all shared fields are main-thread only; guard correctly drops stale callbacks.
- No CancellationToken — declared non-goal. Switch-away relies on the guard; sound.

## Findings (both fixed)
- **Should-fix #1 (blocking):** worker evaluated `MainThreadDispatcher.Instance` — on Editor Stop
  mid-run the getter could create a GameObject from the worker thread (Unity API off-thread),
  violating the core invariant (REQ-005 / AC-006 / profile known-risk #1).
  **Fix:** capture `var dispatcher = MainThreadDispatcher.Instance;` on the main thread before
  `Task.Run`; worker uses the captured reference. (`MainThreadFreezeScenario.RunBackground`.)
- **Should-fix #2:** `Exit()` left `_status = "running…"`, so switch-away-and-back showed enabled
  buttons + "running…" (contradicts AC-007).
  **Fix:** `Exit()` resets `_status`.

## Nice-to-have (not actioned)
- Duplicate concurrent background task possible after switch-away-and-back — result stays correct
  via the guard; only wasted CPU. Benign after #1/#2.
- Per-repaint string/`Rect`/`Color` allocations in `DrawGUI` — consistent with the module; REQ-010
  (GUIStyle caching) is satisfied.

## Resolution
Both Should-fix applied; re-run G2 (compile) → then G5 + G4.
