# Manual QA — main-thread-freeze (Gate G4)

Run in the Unity Editor (Play mode). Primary QA target: Editor / Standalone. WebGL is not a target.

## Setup
1. Open `C:\Projects\ThreadingLab` in Unity 6000.0.66f2 and press **Play**.
2. In the left picker, select **"2 · Main Thread Freeze"**.

## Checks (mapped to acceptance criteria)

- [ ] **AC-001 — Selectable.** "2 · Main Thread Freeze" appears in the picker and selecting it shows its controls (two buttons + a bar).
- [ ] **AC-002 — Idle liveness.** With nothing running, the blue bar sweeps left↔right continuously and the top-strip **FPS** reads the normal rate.
- [ ] **AC-003 — Main-thread run freezes.** Click **Run on Main Thread** → the bar **stops dead**, FPS reads ~0, the window is unresponsive for ~1–3 s, then it snaps back and shows Result + Time + "min FPS during run" (a very low number).
- [ ] **AC-004 — Background run stays smooth.** Click **Run on Background** → the bar **keeps sweeping**, FPS stays high, and Result + Time appear when the work completes (min FPS during run stays high).
- [ ] **AC-005 — No re-entrancy.** During a background run, both buttons are disabled (greyed) and a second run cannot start until it finishes.
- [ ] **AC-006 — No cross-thread errors.** Watch the Console during/after a background run: **no** "can only be called from the main thread" error and **no** unhandled exception.
- [ ] **AC-007 — No leaked worker.** Start a background run, immediately switch to "1 · Race Condition" and back. Then check: buttons are enabled, the status is **not** stuck on "running…", the top-strip **process threads** count returns to its baseline, and the UI is consistent.

## Extra thread-hygiene spot-checks (from the tester lens)
- [ ] Result of the same run is identical between Main and Background modes (same prime count) — confirms a deterministic, equal workload.
- [ ] **Editor Stop mid-run:** start a background run and press **Stop** in the Editor while it computes → no Console error about calling Unity API off the main thread (the G3 fix: dispatcher captured on the main thread).
- [ ] Trigger several background runs back-to-back → no thread-count growth, no stuck workers.

## Result
- Tester: <name> — <date>
- Outcome: PASS / FAIL (list any failing AC)
