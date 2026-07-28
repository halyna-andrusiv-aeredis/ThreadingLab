# Gate G3 — Independent feature review: threadpool-starvation

Reviewer: cold independent `reviewer` subagent.

## Pass 1 — Not approved (1 Must-fix)
- **Must-fix:** `Reset()` zeroed the shared counters while old in-flight items still incremented them →
  a natural Submit → Reset showed `completed > started > submitted`, negative in-flight/backlog
  (violates AC-004's `completed ≤ started ≤ submitted`).
- **Should-fix:** `DrawGUI` read submitted → started → completed (completed last) → a momentary negative
  in-flight even without Reset.
- Verified clean: cancellation/no-runaway (Cancel before Dispose; `ObjectDisposedException` caught);
  `SetMinThreads` global min captured on Enter and restored on Exit (reached via SetActive Exit-before-Enter
  and OnDestroy); no Unity API off the main thread; avoiding `ThreadPool.ThreadCount`/`PendingWorkItemCount`
  is the correct call for Unity's BCL.

## Fixes applied
- **Must-fix:** counters moved into a per-burst `Counters` holder captured at `Submit`; `Reset()` swaps in
  a fresh holder, so stale in-flight items increment the OLD (undisplayed) holder — the new counts can
  never go invalid. `Enter` also allocates a fresh holder.
- **Should-fix:** snapshot now reads completed → started → submitted (each monotonic) → the displayed
  snapshot always satisfies `completed ≤ started ≤ submitted`; no negative in-flight/backlog.
- **Nice-to-have:** `ApplyMinThreads` now warns if `SetMinThreads` is rejected; spec REQ-005 reconciled to
  the busy-workers proxy (no `ThreadCount`).

## Pass 2 — Approved (focused re-review of the fix)
- **Must-fix resolved:** per-burst `Counters` holder — `Submit` captures `var c = _counters`, workers
  close over `c`; `Reset`/`Enter` swap `_counters = new Counters()`. Stale items increment the OLD holder
  (no longer referenced by `_counters`), so `DrawGUI` never observes invalid counts. Per holder,
  `Submitted` is added up-front then each item does `Started++` then `Completed++` → `Completed ≤ Started
  ≤ Submitted` by construction. `_counters` written/read only on the main thread; workers touch only the
  captured local. No residual race.
- **Should-fix resolved:** snapshot reads Completed → Started → Submitted (each monotonic) → invariant
  holds; no negative in-flight/backlog.
- No new concurrency/lifecycle defect; previously-clean items (cancellation, SetMinThreads capture/
  restore, no off-thread Unity API) still clean.
- **Verdict: Approved.**

## Resolution
G3 approved (pass 2). G2 PASS. Proceed to G5 → G4.
