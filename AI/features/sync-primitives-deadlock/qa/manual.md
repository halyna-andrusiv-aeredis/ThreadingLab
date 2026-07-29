# Manual QA — sync-primitives-deadlock (Gate G4)

Run in the Unity Editor. **Open the worktree project `C:\Projects\ThreadingLab-sync`** (not the main
ThreadingLab). Primary QA target: Editor / Standalone. WebGL is not a target.

## Setup
1. Open `C:\Projects\ThreadingLab-sync` in Unity 6000.0.66f2 and press **Play**.
2. In the picker, select **"6 · Deadlock & ReaderWriterLock"**.

## Checks (mapped to acceptance criteria)

- [ ] **AC-001 — Selectable.** The scenario shows both parts: Deadlock (thread states + DEADLOCK flag + Progress + Fix toggle) and Readers/writer (reads/sec, writes/sec + RWLock toggle).
- [ ] **AC-002 — Deadlock occurs.** With Fix **off**, within a moment Thread A and Thread B both show "holds X, wants Y", the **DEADLOCK** flag turns red, and **Progress** stops climbing.
- [ ] **AC-003 — Fix clears it.** Enable **Fix (ordered locks)** → the DEADLOCK flag clears (green "running") and **Progress** climbs steadily.
- [ ] **AC-004 — Readers overlap under RWLock.** With **Use ReaderWriterLockSlim** on, **reads/sec** is high (4 readers overlap); writes/sec ticks along.
- [ ] **AC-005 — Plain lock serializes.** Turn the RWLock toggle **off** (plain lock) → **reads/sec drops clearly** (readers now serialize). Turn it back on → reads/sec jumps up again.
- [ ] **AC-006 — Clean teardown (critical).** While the deadlock is active, switch to another scenario and back a few times → the top-strip **process threads** count returns to baseline (no stuck/leaked threads), no runaway.
- [ ] **AC-007 — No cross-thread errors.** Console shows no "can only be called from the main thread" error and no unhandled exception, including on rapid Fix-toggle / lock-mode-toggle spam.

## Spot-checks (from the G3 review)
- [ ] Spam the **Fix** toggle a few times → watch min-FPS on the top strip for a stall (Restart joins on the main thread; should be brief since `Join` is now 500 ms and workers poll ~25 ms).
- [ ] Spam the **RWLock** toggle → no Console exceptions (it's a live switch, benign transient).

## Result
- Tester: <name> — <date>
- Outcome: PASS / FAIL (list any failing AC)
