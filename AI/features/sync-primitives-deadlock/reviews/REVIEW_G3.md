# Gate G3 — Independent feature review: sync-primitives-deadlock

Reviewer: cold independent `reviewer` subagent (ran automatically alongside G5). Verdict: **Approved**.

## Verified (no Must-fix)
- **Teardown correct:** both deadlock threads and readers/writer take locks via
  `Monitor.TryEnter(lock, PollMs)` in token-checked loops, so even a genuinely deadlocked thread
  re-checks the token every ~25 ms and `Stop()`'s `Join` returns. No thread left blocked. (The one
  thing that would be a hard Must-fix in a threading lab — done right.)
- **Deadlock reliably occurs** (opposite order, hold-first); progress freezes.
- **`ReaderWriterLockSlim` lifetime:** created in `Start`, captured as a local into workers, disposed in
  `Stop` only after joins; Enter/Exit paired in `try/finally`; `catch (ObjectDisposedException)` guards
  the rare join-timeout. Safe.
- No Unity API off the main thread; `Interlocked` counters correct; GUIStyles cached.
- Isolated: one additive host line; no static state.

## Findings applied
- **Should-fix #1 (applied):** the live `_useRwLock` toggle diverged from spec REQ-008 ("restart on
  toggle") and briefly relaxes reader/writer exclusion during the switch. Resolution (option b): keep the
  **live** toggle (instant reads/sec A/B is the pedagogy), add a code comment documenting the transient
  as intentional & benign (atomic int, discarded read), and **update the spec** (REQ-008 + failure
  scenario) so code and spec agree.
- **Should-fix #2 (applied):** `Restart()`'s `Join(2000)` on the main thread from OnGUI could spike FPS.
  Lowered to `Join(500)` (workers poll every ~25 ms).

## Nice-to-have (not actioned)
- `TickCount == 0` sentinel / ~24.9-day wrap in the deadlock detector — cosmetic, display-only.
- Per-frame interpolated strings / unbarriered display reads of `_state[]`/`_waitingSince[]` — atomic,
  display-only, and profile treats demo races as intentional; conscious choice.

## Resolution
Approved by G3; G5 PASS. Should-fixes applied (polish, no threading-model change). Re-run G2 → G4.
