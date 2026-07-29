using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ThreadingLab.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// Atomic operations, mastered — one scenario, three switchable modes, each isolating a facet of
    /// lock-free programming under real contending threads:
    ///
    ///   A · Lock-free counter — Interlocked.Increment vs a hand-rolled CAS loop vs a lock baseline.
    ///   B · Treiber stack     — a lock-free stack whose head is swung with CompareExchange (CAS retries).
    ///   C · False sharing     — two hot counters packed on one cache line vs padded onto separate lines.
    ///
    /// Shared plumbing: a timed RUN HARNESS spawns N worker threads, bounds them by a token +
    /// duration, and JOINS every thread before a run is considered finished. A background "runner" thread
    /// orchestrates each run so the Unity main thread never blocks for the window; workers touch only
    /// atomics / locals / the stop token, and all results are read on the main thread for display.
    ///
    /// Lifecycle discipline mirrors the Producer/Consumer scenario: a CancellationTokenSource stops the
    /// work and Exit()/mode-switch cancels + joins — no leaked or runaway workers.
    ///
    /// Matrix cell: master atomic operations (lock-free coordination, CAS loops, false sharing).
    /// </summary>
    public sealed class AtomicMasteryScenario : IThreadingScenario
    {
        public string Title => "6 · Atomic Mastery";
        public string Description =>
            "Lock-free coordination with atomics: Interlocked vs CAS loop vs lock, a CAS-based Treiber " +
            "stack, and the false-sharing throughput cliff — each under real contending threads.";

        private enum Mode { LockFreeCounter, TreiberStack, FalseSharing }
        private static readonly string[] ModeLabels = { "A · Lock-free counter", "B · Treiber stack", "C · False sharing" };

        private Mode _mode = Mode.LockFreeCounter;

        // Common controls. Thread count applies to modes A/B; mode C always uses exactly two threads.
        private int _threads = 8;
        private int _runMs = 500;

        // Run state. _isRunning is volatile: the runner writes results, THEN clears this flag, so a main
        // thread that observes it flip false also sees the freshly written results (publish/subscribe).
        private volatile bool _isRunning;
        private Thread _runner;                 // orchestrates one run off the main thread
        private CancellationTokenSource _cts;

        // ── Mode A: lock-free counter ────────────────────────────────────────────────────────────────
        // One shared counter hammered by every worker; three strategies compared over the same window.
        private long _counter;
        private readonly object _gate = new object();
        private StrategyResult[] _modeAResults;   // published by the runner; read on the main thread

        private struct StrategyResult
        {
            public string Name;
            public long Final;      // the counter's value after the run
            public long Expected;   // total increments the workers actually performed
            public double OpsPerSec;
            public bool Correct;    // Final == Expected → no lost updates
        }

        // ── Mode B: Treiber stack ────────────────────────────────────────────────────────────────────
        private long _pushed;
        private long _popped;
        private StackResult? _modeBResult;    // published by the runner; read on the main thread

        private struct StackResult
        {
            public long Pushed, Popped, Retries;
            public int Remaining;
            public double OpsPerSec;
            public bool Conserved;            // Pushed == Popped + Remaining → nothing lost/duplicated
        }

        // ── Mode C: false sharing ────────────────────────────────────────────────────────────────────
        // Two hot counters, two threads. Packed puts them 8 bytes apart (same 64-byte cache line);
        // padded puts them 128 bytes apart (separate lines, clear of adjacent-line prefetch). Counters
        // live in a heap-allocated holder so each thread can take a stable `ref` to its own field.
        private bool _paddedLayout;           // false = packed (the cliff), true = padded
        private double _packedOps, _paddedOps; // last cached ops/s per layout (0 = not measured yet)

        [StructLayout(LayoutKind.Explicit)]
        private struct PackedCounters
        {
            [FieldOffset(0)] public long A;
            [FieldOffset(8)] public long B;   // adjacent → shares A's cache line (false sharing)
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PaddedCounters
        {
            [FieldOffset(0)]   public long A;
            [FieldOffset(128)] public long B; // two cache lines away → no sharing
        }

        private sealed class PackedHolder { public PackedCounters C; }
        private sealed class PaddedHolder { public PaddedCounters C; }

        private GUIStyle _big, _mid, _tip;
        private GUIStyle _modeOn, _modeOff;

        public void Enter()
        {
            _isRunning = false;
        }

        public void Exit()
        {
            StopRun();
        }

        public void Tick(float deltaTime) { }

        // ── Run harness ─────────────────────────────────────────────────────────────────────────────

        // Start a run of the active mode. No-op while a run is in flight (one run at a time).
        private void Run()
        {
            if (_isRunning) return;
            StopRun();                          // ensure any finished runner is joined + disposed first
            _cts = new CancellationTokenSource(); // cancelled by Exit()/mode-switch; the per-segment
                                                  // window is a linked CTS inside RunTimedSegment
            _isRunning = true;
            var ct = _cts.Token;
            _runner = new Thread(() => RunActiveMode(ct)) { IsBackground = true, Name = "AtomicMastery-Runner" };
            _runner.Start();
        }

        // Cancel the active run and JOIN the runner (which in turn joins its workers) — unconditional,
        // used by Exit() and mode-switch. Safe to call when nothing is running.
        private void StopRun()
        {
            _cts?.Cancel();
            if (_runner != null && !_runner.Join(2000))
                Debug.LogWarning("AtomicMastery: the runner did not stop within the join timeout.");
            _runner = null;
            _cts?.Dispose();
            _cts = null;
            _isRunning = false;
        }

        // Runs on the background runner thread. Dispatches to the active mode, then publishes results by
        // clearing _isRunning last (see the volatile note above).
        private void RunActiveMode(CancellationToken ct)
        {
            try
            {
                switch (_mode)
                {
                    case Mode.LockFreeCounter: RunLockFreeCounter(ct); break;
                    case Mode.TreiberStack:    RunTreiberStack(ct);    break;
                    case Mode.FalseSharing:    RunFalseSharing(ct);    break;
                }
            }
            catch (Exception e)
            {
                // Debug.Log* is one of the few thread-safe Unity APIs — deliberately allowed off the main
                // thread here for the single failure path. Do NOT add other UnityEngine calls beside it.
                Debug.LogWarning($"AtomicMastery run failed: {e}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        // One timed segment: run <count> workers for a _runMs window (a linked CTS so Exit()/mode-switch
        // still cancels early), join them, and report elapsed ms. Modes call this once (B) or several
        // times in sequence (A runs three strategies, C runs packed then padded) — each gets a full window.
        private long RunTimedSegment(int count, CancellationToken ct, Func<int, CancellationToken, long> body, out double elapsedMs)
        {
            using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
            window.CancelAfter(_runMs);
            var sw = Stopwatch.StartNew();
            long total = RunTimedWorkers(count, window.Token, body);
            sw.Stop();
            elapsedMs = sw.Elapsed.TotalMilliseconds;
            return total;
        }

        // Reusable core: spawn <count> worker threads, each running <body> until the token trips, JOIN
        // them all, and return the summed per-thread op counts. No Unity API on these threads. All state
        // here is LOCAL — nothing is shared with the main thread, so even a stray second runner could not
        // corrupt it (each per-thread op count is written once, then read after Join → happens-before).
        private long RunTimedWorkers(int count, CancellationToken ct, Func<int, CancellationToken, long> body)
        {
            var ops = new long[count];
            var workers = new Thread[count];
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                workers[i] = new Thread(() => ops[idx] = body(idx, ct)) { IsBackground = true, Name = $"AtomicMastery-W{idx}" };
                workers[i].Start();
            }
            foreach (var t in workers) t.Join();

            long total = 0;
            for (int i = 0; i < count; i++) total += ops[i];
            return total;
        }

        // ── Modes ────────────────────────────────────────────────────────────────────────────────────

        // Mode A: run three increment strategies over the same thread count + window and compare
        // correctness (final == expected) and throughput. All three are correct; the lock-free paths win.
        private void RunLockFreeCounter(CancellationToken ct)
        {
            var results = new StrategyResult[3];

            // Interlocked.Increment — a single atomic increment instruction.
            results[0] = RunStrategy("Interlocked.Increment", ct, (idx, token) =>
            {
                long n = 0;
                while (!token.IsCancellationRequested) { Interlocked.Increment(ref _counter); n++; }
                return n;
            });

            // CAS loop — the same increment spelled out as read → compute → CompareExchange → retry.
            results[1] = RunStrategy("CAS loop (CompareExchange)", ct, (idx, token) =>
            {
                long n = 0;
                while (!token.IsCancellationRequested)
                {
                    long old;
                    do { old = Volatile.Read(ref _counter); }
                    while (Interlocked.CompareExchange(ref _counter, old + 1, old) != old);
                    n++;
                }
                return n;
            });

            // lock baseline — correct, but the mutex serializes every increment.
            results[2] = RunStrategy("lock (baseline)", ct, (idx, token) =>
            {
                long n = 0;
                while (!token.IsCancellationRequested) { lock (_gate) { _counter++; } n++; }
                return n;
            });

            _modeAResults = results;
        }

        // Reset the shared counter, run one strategy for a full window, and measure correctness + ops/sec.
        private StrategyResult RunStrategy(string name, CancellationToken ct, Func<int, CancellationToken, long> body)
        {
            Interlocked.Exchange(ref _counter, 0);
            long expected = RunTimedSegment(_threads, ct, body, out double ms);
            long final = Interlocked.Read(ref _counter);
            double ops = ms > 0 ? final / (ms / 1000.0) : 0;
            return new StrategyResult { Name = name, Final = final, Expected = expected, OpsPerSec = ops, Correct = final == expected };
        }

        // Mode B: workers push then pop through one lock-free Treiber stack whose head is swung by CAS.
        // Track pushed/popped/retries and verify pushed == popped + remaining after everyone joins.
        private void RunTreiberStack(CancellationToken ct)
        {
            var stack = new TreiberStack();
            Interlocked.Exchange(ref _pushed, 0);
            Interlocked.Exchange(ref _popped, 0);

            long ops = RunTimedSegment(_threads, ct, (idx, token) =>
            {
                long n = 0;
                long val = (long)idx << 40;   // per-thread value range, so pushed values are distinct
                while (!token.IsCancellationRequested)
                {
                    stack.Push(val++);
                    Interlocked.Increment(ref _pushed);
                    n++;
                    if (stack.TryPop(out _)) { Interlocked.Increment(ref _popped); n++; }
                }
                return n;
            }, out double ms);

            long pushed = Interlocked.Read(ref _pushed);
            long popped = Interlocked.Read(ref _popped);
            int remaining = stack.CountRemaining();   // no concurrent mutation after join — safe to walk
            double opsPerSec = ms > 0 ? ops / (ms / 1000.0) : 0;

            _modeBResult = new StackResult
            {
                Pushed = pushed,
                Popped = popped,
                Retries = stack.Retries,
                Remaining = remaining,
                OpsPerSec = opsPerSec,
                Conserved = pushed == popped + remaining,
            };
        }

        // Mode C: two threads hammer two counters with Interlocked.Increment (a locked memory op every
        // iteration, so the traffic actually hits the cache line). The selected layout decides whether the
        // counters share a line; the same run on the padded layout frees them — that gap is the cliff.
        private void RunFalseSharing(CancellationToken ct)
        {
            double ms;
            long total;
            if (_paddedLayout)
            {
                var h = new PaddedHolder();
                total = RunTimedSegment(2, ct, (idx, token) =>
                {
                    long n = 0;
                    if (idx == 0) while (!token.IsCancellationRequested) { Interlocked.Increment(ref h.C.A); n++; }
                    else          while (!token.IsCancellationRequested) { Interlocked.Increment(ref h.C.B); n++; }
                    return n;
                }, out ms);
                _paddedOps = ms > 0 ? total / (ms / 1000.0) : 0;
            }
            else
            {
                var h = new PackedHolder();
                total = RunTimedSegment(2, ct, (idx, token) =>
                {
                    long n = 0;
                    if (idx == 0) while (!token.IsCancellationRequested) { Interlocked.Increment(ref h.C.A); n++; }
                    else          while (!token.IsCancellationRequested) { Interlocked.Increment(ref h.C.B); n++; }
                    return n;
                }, out ms);
                _packedOps = ms > 0 ? total / (ms / 1000.0) : 0;
            }
        }

        // ── UI ───────────────────────────────────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_big != null) return; // build once — no per-frame GUIStyle allocations
            _big = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _tip = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            _modeOn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            _modeOff = new GUIStyle(GUI.skin.button);
        }

        public void DrawGUI(Rect area)
        {
            EnsureStyles();

            // Snapshot the state that drives control COUNT once per pass. _isRunning is volatile, so this
            // acquire read is sequenced before every data read below (fixes publication order), and driving
            // all layout off these locals — while DEFERRING any state change a button requests until after
            // EndArea — keeps the IMGUI Layout and Repaint passes emitting the same controls (no mismatch).
            bool running = _isRunning;
            Mode mode = _mode;
            Mode? pendingMode = null;
            bool pendingRun = false, pendingReset = false;

            GUILayout.BeginArea(area);

            // Mode selector — the switch (cancel + join + change mode) is applied after EndArea.
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ModeLabels.Length; i++)
            {
                bool on = (int)mode == i;
                if (GUILayout.Button(ModeLabels[i], on ? _modeOn : _modeOff, GUILayout.Height(28), GUILayout.Width(180)))
                {
                    if (!on) pendingMode = (Mode)i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            bool modeUsesThreadCount = mode != Mode.FalseSharing;
            if (modeUsesThreadCount)
                _threads = Stepper("Threads", _threads, 1, 64, 1);
            else
                GUILayout.Label("Threads: <b>2</b> (one per counter)", _mid);
            _runMs = Stepper("Run ms", _runMs, 100, 3000, 100);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            using (new GuiEnabled(!running))
            {
                if (GUILayout.Button(running ? "Running…" : "Run", GUILayout.Height(34), GUILayout.Width(200)))
                    pendingRun = true;
            }
            if (GUILayout.Button("Reset", GUILayout.Height(34), GUILayout.Width(90)))
                pendingReset = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);

            switch (mode)
            {
                case Mode.LockFreeCounter: DrawLockFreeCounter(running); break;
                case Mode.TreiberStack:    DrawTreiberStack(running);    break;
                case Mode.FalseSharing:    DrawFalseSharing(running);    break;
            }

            GUILayout.EndArea();

            // Apply deferred state changes now that the layout group is closed — never mid-pass.
            if (pendingReset) Reset();
            if (pendingMode.HasValue) { StopRun(); _mode = pendingMode.Value; }
            if (pendingRun) Run();
        }

        private void DrawLockFreeCounter(bool running)
        {
            GUILayout.Label($"Same <b>{_threads}</b> threads · <b>{_runMs}</b> ms window — three strategies, one Run:", _mid);
            GUILayout.Space(6f);

            var r = _modeAResults;
            if (running || r == null)
            {
                GUILayout.Label(running ? "Running…" : "Press <b>Run</b>.", _big);
            }
            else
            {
                double max = 1d;
                foreach (var s in r) if (s.OpsPerSec > max) max = s.OpsPerSec;
                foreach (var s in r) DrawStrategyRow(s, max);
            }

            GUILayout.Space(14f);
            GUILayout.Label(
                "All three strategies land the exact same final count (no lost updates) — but ops/sec " +
                "differs sharply. Interlocked.Increment is the CAS loop compiled to one atomic instruction; " +
                "the explicit CAS loop does the same read → CompareExchange → retry by hand. The lock is " +
                "correct too, yet its mutex serializes every increment, so it trails under contention.",
                _tip);
        }

        private void DrawStrategyRow(StrategyResult s, double max)
        {
            GUILayout.Label(
                $"<b>{s.Name}</b> — <b>{s.OpsPerSec:N0}</b> ops/s   " +
                $"(final {s.Final:N0} vs expected {s.Expected:N0} · " +
                $"<color={(s.Correct ? "lime" : "red")}>{(s.Correct ? "correct" : "LOST UPDATES")}</color>)",
                _mid);
            var bar = GUILayoutUtility.GetRect(360f, 16f, GUILayout.ExpandWidth(false));
            GUI.Box(bar, GUIContent.none);
            float frac = max > 0 ? Mathf.Clamp01((float)(s.OpsPerSec / max)) : 0f;
            var prev = GUI.color;
            GUI.color = new Color(0.4f, 0.8f, 1f);
            GUI.Box(new Rect(bar.x, bar.y, bar.width * frac, bar.height), GUIContent.none);
            GUI.color = prev;
            GUILayout.Space(6f);
        }

        private void DrawTreiberStack(bool running)
        {
            GUILayout.Label($"<b>{_threads}</b> threads push then pop through one atomic head · <b>{_runMs}</b> ms window:", _mid);
            GUILayout.Space(6f);

            var rn = _modeBResult;
            if (running || rn == null)
            {
                GUILayout.Label(running ? "Running…" : "Press <b>Run</b>.", _big);
            }
            else
            {
                var r = rn.Value;
                GUILayout.Label($"Throughput: <b>{r.OpsPerSec:N0}</b> ops/s", _big);
                GUILayout.Space(4f);
                GUILayout.Label($"CAS retries (contention): <b>{r.Retries:N0}</b>", _mid);
                GUILayout.Space(4f);
                GUILayout.Label(
                    $"Pushed <b>{r.Pushed:N0}</b>  =  Popped <b>{r.Popped:N0}</b>  +  Remaining <b>{r.Remaining:N0}</b>   →   " +
                    $"<color={(r.Conserved ? "lime" : "red")}>{(r.Conserved ? "conserved" : "VIOLATED")}</color>",
                    _mid);
            }

            GUILayout.Space(14f);
            GUILayout.Label(
                "Push and Pop swing the shared head with Interlocked.CompareExchange: read the head, prepare " +
                "the new one, and commit only if nobody moved it meanwhile — otherwise re-read and retry. " +
                "Retries climb with thread count: that is contention made visible, with no lock taken. The " +
                "conservation line proves nothing was lost or double-popped. (A pointer-only Treiber stack " +
                "is ABA-prone in general; here nodes are fresh allocations that aren't recycled mid-run.)",
                _tip);
        }

        private void DrawFalseSharing(bool running)
        {
            using (new GuiEnabled(!running))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Layout:", _mid, GUILayout.Width(96f));
                if (GUILayout.Button("Packed (one line)", _paddedLayout ? _modeOff : _modeOn, GUILayout.Width(170), GUILayout.Height(26)))
                    _paddedLayout = false;
                if (GUILayout.Button("Padded (separate lines)", _paddedLayout ? _modeOn : _modeOff, GUILayout.Width(200), GUILayout.Height(26)))
                    _paddedLayout = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            if (running)
                GUILayout.Label("Running…", _big);
            else
                GUILayout.Label("Total throughput (run each layout once):", _mid);

            GUILayout.Space(6f);
            double max = Math.Max(_packedOps, _paddedOps);
            if (max <= 0) max = 1d;
            DrawLayoutBar("Packed (one cache line)", _packedOps, max, new Color(1f, 0.6f, 0.2f));
            DrawLayoutBar("Padded (separate lines)", _paddedOps, max, new Color(0.4f, 0.8f, 1f));

            GUILayout.Space(10f);
            GUILayout.Label(
                "Both threads only ever touch their own counter — there is no logical sharing. But when the " +
                "two counters sit on one 64-byte cache line (packed), every increment on one invalidates the " +
                "other core's copy of the whole line, so the CPUs ping-pong it back and forth: throughput " +
                "collapses. Padding them onto separate lines removes the contention and the same run speeds " +
                "up sharply — the cliff. Magnitude is hardware/JIT dependent; the direction is the point.",
                _tip);
        }

        private void DrawLayoutBar(string name, double ops, double max, Color color)
        {
            GUILayout.Label(ops > 0 ? $"<b>{name}</b> — <b>{ops:N0}</b> ops/s" : $"<b>{name}</b> — (not run yet)", _mid);
            var bar = GUILayoutUtility.GetRect(360f, 18f, GUILayout.ExpandWidth(false));
            GUI.Box(bar, GUIContent.none);
            float frac = max > 0 ? Mathf.Clamp01((float)(ops / max)) : 0f;
            var prev = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(bar.x, bar.y, bar.width * frac, bar.height), GUIContent.none);
            GUI.color = prev;
            GUILayout.Space(8f);
        }

        private void Reset()
        {
            if (_isRunning) return; // don't clear results out from under a live run
            _modeAResults = null;
            _modeBResult = null;
            _packedOps = 0;
            _paddedOps = 0;
        }

        private int Stepper(string label, int value, int min, int max, int step)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(260f));
            GUILayout.Label(label, _mid, GUILayout.Width(96f));
            if (GUILayout.Button("-", GUILayout.Width(28f))) value = Mathf.Max(min, value - step);
            GUILayout.Label(value.ToString(), _mid, GUILayout.Width(56f));
            if (GUILayout.Button("+", GUILayout.Width(28f))) value = Mathf.Min(max, value + step);
            GUILayout.EndHorizontal();
            return value;
        }

        // A lock-free stack (Treiber's algorithm): the head pointer is the only shared state, swung with
        // Interlocked.CompareExchange in a retry loop. Nodes are fresh `new` allocations that are never
        // recycled during a run, which sidesteps the classic ABA hazard (a freed address reappearing).
        private sealed class TreiberStack
        {
            private sealed class Node { public long Value; public Node Next; }

            private Node _head;
            private long _retries;

            public void Push(long value)
            {
                var node = new Node { Value = value };
                while (true)
                {
                    Node head = Volatile.Read(ref _head);
                    node.Next = head;
                    // Publish the new head only if nobody moved it since we read it.
                    if (Interlocked.CompareExchange(ref _head, node, head) == head) return;
                    Interlocked.Increment(ref _retries); // someone raced us — re-read and retry
                }
            }

            public bool TryPop(out long value)
            {
                while (true)
                {
                    Node head = Volatile.Read(ref _head);
                    if (head == null) { value = 0; return false; }
                    Node next = head.Next;
                    if (Interlocked.CompareExchange(ref _head, next, head) == head) { value = head.Value; return true; }
                    Interlocked.Increment(ref _retries);
                }
            }

            public long Retries => Interlocked.Read(ref _retries);

            // Walk the chain — call only after all workers have joined (no concurrent mutation).
            public int CountRemaining()
            {
                int c = 0;
                for (Node n = Volatile.Read(ref _head); n != null; n = n.Next) c++;
                return c;
            }
        }

        // Small RAII helper so a disabled Run button reads clean at the call site.
        private readonly struct GuiEnabled : IDisposable
        {
            private readonly bool _prev;
            public GuiEnabled(bool enabled) { _prev = GUI.enabled; GUI.enabled = enabled; }
            public void Dispose() { GUI.enabled = _prev; }
        }
    }
}
