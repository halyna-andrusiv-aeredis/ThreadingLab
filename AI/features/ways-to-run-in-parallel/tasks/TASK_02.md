# Task 02 — Parallel.For + ThreadPool methods + selector + weight control

## Goal
Add the `Parallel.For` and `ThreadPool` (`CountdownEvent`) compute methods over disjoint row
bands, a live method selector, and a resolution-step weight control, so the user can compare
compute-ms/frame and FPS across the three methods at the same workload.

## Traceability
- **requirements:** REQ-003, REQ-005, REQ-006, REQ-010
- **acceptance:** AC-004, AC-005, AC-006, AC-007, AC-008

## Files allowed to touch
- Assets/Scripts/Scenarios/WaysToRunInParallelScenario.cs (extend)

## Acceptance
- [ ] Method selector switches Sequential / Parallel.For / ThreadPool live (next frame, no restart).
- [ ] `Parallel.For` partitions rows across workers; main thread waits before the texture upload.
- [ ] `ThreadPool` partitions rows into `QueueUserWorkItem` chunks and waits via `CountdownEvent`.
- [ ] Each worker writes a disjoint row band — no shared-write race.
- [ ] At high weight, Parallel.For and ThreadPool show lower compute-ms/frame and higher FPS than Sequential.
- [ ] The image is identical across methods for the same time value.
- [ ] Resolution-step weight control (128/256/512/768) changes the workload; buffer/texture realloc safely.
- [ ] No "main thread only" errors or exceptions in the Console; no texture/thread leak on re-entry.
- [ ] Compiles clean (G2).
