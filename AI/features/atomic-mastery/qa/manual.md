# Manual QA — atomic-mastery (Gate G4)

Run in the Unity Editor (Play mode). Primary QA target: Editor / Standalone. WebGL is not a target
(single-threaded — the demos cannot run there).

## Setup
1. Open `C:\Projects\ThreadingLab` in Unity 6000.0.66f2 and press **Play**.
   (Note: this feature was built in the `feature/atomic-mastery` git worktree at
   `C:\Projects\ThreadingLab-atomic` — open whichever checkout has the branch.)
2. In the left picker, select **"6 · Atomic Mastery"**.
3. You should see a mode selector (**A · Lock-free counter / B · Treiber stack / C · False sharing**),
   the **Threads** + **Run ms** steppers, and **Run** / **Reset** buttons.

## Checks (mapped to acceptance criteria)

- [ ] **AC-001 — Selectable.** "6 · Atomic Mastery" appears in the picker; selecting it shows the mode
  selector + controls (no errors on select).

### Mode A — Lock-free counter
- [ ] **AC-002 — Correctness.** Set **Threads** high (e.g. 16–32), **Run ms** ~500, press **Run**. When it
  finishes, each of the three rows (Interlocked.Increment / CAS loop / lock) shows **final == expected**
  and a green **correct** tag (never red "LOST UPDATES").
- [ ] **AC-003 — Lock-free throughput.** In the same result, the **Interlocked.Increment** and **CAS loop**
  rows report a higher **ops/s** (and longer bars) than the **lock (baseline)** row. The gap widens as you
  raise the thread count.

### Mode B — Treiber stack
- [ ] **AC-004 — CAS retries + conservation.** Switch to **B · Treiber stack**, set several threads (e.g. 8),
  press **Run**. After it finishes: **CAS retries** is > 0 (rises with more threads), and the line
  **Pushed = Popped + Remaining** shows a green **conserved** tag (never red "VIOLATED").

### Mode C — False sharing
- [ ] **AC-005 — The cliff.** Switch to **C · False sharing** (Threads shows a fixed "2"). With **Packed
  (one line)** selected, press **Run** and note the Packed ops/s bar. Switch the layout toggle to **Padded
  (separate lines)** and press **Run** again → the **Padded** total ops/s is noticeably higher than
  **Packed** (the two-bar contrast makes the cliff visible). Magnitude varies by CPU — the *direction*
  (padded > packed) is what must hold.

### Cross-cutting
- [ ] **AC-006 — Clean teardown / no runaway.** Start a run (ideally raise **Run ms** to ~3000 so it is
  clearly in flight), then immediately **switch mode** or **select another scenario in the picker**. The
  top-strip **process thread count** must settle back to baseline within a moment — no thread keeps
  spinning (watch the FPS strip stay healthy). Repeat a few times.
- [ ] **AC-007 — No cross-thread / IMGUI errors.** Throughout all of the above — including clicking **Run**,
  the **mode buttons**, the **layout toggle**, and **Reset** repeatedly — the Console shows **no**
  "can only be called from the main thread" error, **no** "Mismatched LayoutGroup" / `ArgumentException`
  from IMGUI, and no unhandled exception. (A `Debug.LogWarning "AtomicMastery run failed…"` should not
  appear in normal use.)
- [ ] **Run-guard sanity.** While a run is in flight the **Run** button reads "Running…" and is disabled;
  **Reset** does not clear results mid-run.

## Result
- Tester: user — 2026-07-28
- Outcome: **PASS** (all AC-001..007 confirmed in the Editor).
