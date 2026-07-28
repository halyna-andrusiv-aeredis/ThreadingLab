# Manual QA — ways-to-run-in-parallel (Gate G4)

Run in the Unity Editor (Play mode). Primary QA target: Editor / Standalone. WebGL is not a target.
Payload is the authentic @yuruyurau particle "creature" (CR-01).

## Setup
1. Open `C:\Projects\ThreadingLab` in Unity 6000.0.66f2 and press **Play**.
2. In the left picker, select **"3 · Ways to Run in Parallel"**.

## Checks (mapped to acceptance criteria)

- [ ] **AC-001 — Selectable.** The scenario appears; selecting it shows a red particle image + Method / Points controls + a metrics line. No garbage flash on first show.
- [ ] **AC-002 — Animates.** The red "creature" (point cloud) visibly pulsates/moves over time.
- [ ] **AC-003 — Sequential is the slow baseline.** Set **Points = 2M or 4M**, **Method = Sequential** → FPS drops and "Compute ms/frame" is high.
- [ ] **AC-004 — Parallel.For speeds it up.** At the same point count, click **Parallel.For** → compute-ms/frame drops noticeably (toward sequential ÷ cores) and FPS rises.
- [ ] **AC-005 — ThreadPool speeds it up.** Click **ThreadPool** → compute-ms/frame and FPS are in the same improved ballpark as Parallel.For.
- [ ] **AC-006 — Identical image, live switch.** Switching method changes only the metrics; the creature shape stays identical, no restart/flash.
- [ ] **AC-007 — No cross-thread errors.** Console: **no** "can only be called from the main thread" error and **no** unhandled exception while a parallel method runs.
- [ ] **AC-008 — No leak on re-entry.** Switch away and back several times, change Points a few times → top-strip **process threads** count does not keep growing; no memory blowup.

## Visual / payload checks
- [ ] The image actually looks like an **organic creature / point cloud** (not a symmetric mandala).
- [ ] **Centering:** the creature is roughly centered and reasonably sized in the 512×512 view (auto-fit was tuned blind — if it is off-center, clipped, tiny, or huge, note it and I will adjust `_fitScale` / offsets).
- [ ] At **Points = 250k** (light) the parallel methods may be **no faster / slower** than Sequential — confirm and understand: at small workloads the partition + per-thread-buffer **merge overhead** dominates (the "downside" — a scatter job is not free to parallelize).

## Result
- Tester: <name> — <date>
- Outcome: PASS / FAIL (list any failing AC)
