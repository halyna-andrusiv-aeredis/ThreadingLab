# Manual QA — threadpool-starvation (Gate G4)

Run in the Unity Editor (Play mode). Primary QA target: Editor / Standalone. WebGL is not a target.

## Setup
1. Open `C:\Projects\ThreadingLab` in Unity 6000.0.66f2 and press **Play**.
2. In the left picker, select **"5 · ThreadPool Starvation"**.

## Checks (mapped to acceptance criteria)

- [ ] **AC-001 — Selectable.** The scenario appears; selecting it shows counters, the ThreadPool state line, the backlog bar, and steppers (Burst / Block ms / Min threads) + Submit + Reset.
- [ ] **AC-002 — Starvation (default min).** Keep **Min threads** at default, set **Block ms** ~1000 and **Burst** ~64, press **Submit burst** → **Started** plateaus near core count, the **Backlog** bar stays orange (high), **Completed** rises slowly, and **Pool busy workers** creeps up ~1/sec over several seconds.
- [ ] **AC-003 — Mitigation.** Raise **Min threads** to well above the burst (e.g. 64–128), press **Reset**, then **Submit burst** again → nearly all items **Start** at once, the backlog bar drops to ~0, and the burst completes quickly.
- [ ] **AC-004 — Counters consistent.** At all times Completed ≤ Started ≤ Submitted; In-flight (= Started − Completed) and Backlog (= Submitted − Started) are never negative. **Try the natural Submit → Reset while items are in flight** — the counters must NOT show negative in-flight or completed > submitted (this was the G3 fix).
- [ ] **AC-005 — Clean teardown.** With items in flight, switch to another scenario → no runaway; the top-strip **process threads** count settles back. The **pool min** is restored (select the scenario again and confirm 'pool min' shows the original value, not the raised one).
- [ ] **AC-006 — No cross-thread errors.** Console shows no "can only be called from the main thread" error and no unhandled exception. (A `Debug.LogWarning` only if SetMinThreads is ever rejected — should not normally appear.)

## Result
- Tester: <name> — <date>
- Outcome: PASS / FAIL (list any failing AC)
