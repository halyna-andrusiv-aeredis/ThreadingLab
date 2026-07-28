using System;
using System.Threading;
using ThreadingLab.Core;
using UnityEngine;

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// ThreadPool starvation, made visible — the dangerous cousin of the idle-consumer starvation in
    /// the Producer/Consumer scenario.
    ///
    /// A "Submit" queues a burst of work items on the SHARED <see cref="ThreadPool"/>. Each item BLOCKS
    /// its pool thread (a cancellable wait) for a while. With the default pool minimum only ~core-count
    /// items start immediately; the rest sit in the pool's queue (backlog) because there are no free
    /// pool threads, and the pool injects new threads only ~1/second. Raising the pool minimum
    /// (`SetMinThreads`) is the standard mitigation.
    ///
    /// Unlike the Producer/Consumer scenario (dedicated `new Thread()` workers), this uses the shared
    /// pool — which is exactly why blocking its threads hurts everyone.
    ///
    /// Matrix cell: basic understanding of ThreadPool starvation and its cause (blocking pool threads).
    /// </summary>
    public sealed class ThreadPoolStarvationScenario : IThreadingScenario
    {
        public string Title => "5 · ThreadPool Starvation";
        public string Description =>
            "Submit a burst of BLOCKING work items to the shared ThreadPool and watch it starve — then " +
            "raise the pool minimum (SetMinThreads) and watch the same burst run at once.";

        private int _burst = 64;
        private int _blockMs = 1000;
        private int _minThreads; // target worker minimum for SetMinThreads (init from the original)

        private CancellationTokenSource _cts;

        // Counters live in a holder captured per-burst. Reset() swaps in a fresh holder, so stale
        // in-flight items keep incrementing the OLD (no-longer-displayed) holder and can never corrupt
        // the fresh counts — keeps completed ≤ started ≤ submitted (AC-004) under Submit → Reset.
        private sealed class Counters { public long Submitted, Started, Completed; }
        private Counters _counters = new Counters();

        // Original pool minimum, captured on Enter and restored on Exit (SetMinThreads is process-global).
        private int _origMinWorker, _origMinIo;

        private GUIStyle _big, _mid, _tip;

        public void Enter()
        {
            ThreadPool.GetMinThreads(out _origMinWorker, out _origMinIo);
            _minThreads = _origMinWorker;
            _cts = new CancellationTokenSource();
            _counters = new Counters();
        }

        public void Exit()
        {
            _cts?.Cancel();                                       // in-flight blocking waits return promptly
            ThreadPool.SetMinThreads(_origMinWorker, _origMinIo); // restore the global pool minimum
            _cts?.Dispose();
            _cts = null;
        }

        public void Tick(float deltaTime) { }

        private void Submit()
        {
            if (_cts == null) return;
            var ct = _cts.Token;
            var c = _counters;              // this burst's counter holder
            int n = _burst;
            Interlocked.Add(ref c.Submitted, n);
            for (int i = 0; i < n; i++)
                ThreadPool.QueueUserWorkItem(_ => WorkItem(ct, c));
        }

        // Cancel in-flight items and swap in a fresh counter holder (stale items keep touching the old
        // one, which is no longer displayed — no corruption of the new counts).
        private void Reset()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _counters = new Counters();
        }

        private void ApplyMinThreads()
        {
            // Only the worker minimum is the point here; keep the original IO minimum.
            if (!ThreadPool.SetMinThreads(_minThreads, _origMinIo))
                Debug.LogWarning($"ThreadPool.SetMinThreads({_minThreads}, {_origMinIo}) was rejected.");
        }

        // A single pool work item: it BLOCKS its pool thread for _blockMs (cancellable). Holding the
        // thread is the whole point — that is what starves the pool. Increments its own burst's holder.
        private void WorkItem(CancellationToken ct, Counters c)
        {
            Interlocked.Increment(ref c.Started);
            try
            {
                if (!ct.IsCancellationRequested)
                    ct.WaitHandle.WaitOne(_blockMs);
            }
            catch (ObjectDisposedException) { } // CTS disposed during teardown — safe to ignore
            Interlocked.Increment(ref c.Completed);
        }

        private void EnsureStyles()
        {
            if (_big != null) return; // build once — no per-frame GUIStyle allocations
            _big = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _tip = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
        }

        public void DrawGUI(Rect area)
        {
            EnsureStyles();
            GUILayout.BeginArea(area);

            _burst = Stepper("Burst", _burst, 8, 256, 8);
            _blockMs = Stepper("Block ms", _blockMs, 100, 3000, 100);
            int min = Stepper("Min threads", _minThreads, _origMinWorker, 256, 8);
            if (min != _minThreads) { _minThreads = min; ApplyMinThreads(); }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Submit burst ({_burst} × {_blockMs} ms)", GUILayout.Height(36), GUILayout.Width(300)))
                Submit();
            if (GUILayout.Button("Reset", GUILayout.Height(36), GUILayout.Width(90)))
                Reset();
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);

            // Read in completed -> started -> submitted order: since each only rises, this snapshot
            // always satisfies completed <= started <= submitted (no negative in-flight/backlog).
            var c = _counters;
            long completed = Interlocked.Read(ref c.Completed);
            long started = Interlocked.Read(ref c.Started);
            long submitted = Interlocked.Read(ref c.Submitted);
            long inFlight = started - completed;
            long backlog = submitted - started;

            GUILayout.Label($"Backlog (queued, not started): <b>{backlog}</b>", _big);

            var bar = GUILayoutUtility.GetRect(320f, 22f, GUILayout.ExpandWidth(false));
            GUI.Box(bar, GUIContent.none);
            float frac = submitted > 0 ? Mathf.Clamp01(backlog / (float)submitted) : 0f;
            var prev = GUI.color;
            GUI.color = backlog > 0 ? new Color(1f, 0.6f, 0.2f) : new Color(0.4f, 0.8f, 1f);
            GUI.Box(new Rect(bar.x, bar.y, bar.width * frac, bar.height), GUIContent.none);
            GUI.color = prev;

            GUILayout.Space(10f);
            GUILayout.Label(
                $"Submitted: <b>{submitted}</b>   |   Started: <b>{started}</b>   |   " +
                $"Completed: <b>{completed}</b>   |   In-flight: <b>{inFlight}</b>",
                _mid);

            ThreadPool.GetMaxThreads(out int maxW, out _);
            ThreadPool.GetAvailableThreads(out int availW, out _);
            ThreadPool.GetMinThreads(out int curMinW, out _);
            int busy = maxW - availW;
            GUILayout.Label(
                $"Pool busy workers: <b>{busy}</b>   |   pool min: <b>{curMinW}</b>   |   cores: {SystemInfo.processorCount}",
                _mid);

            GUILayout.Space(14f);
            GUILayout.Label(
                "Default 'Min threads' → a blocking burst starves: Started plateaus near core count, the " +
                "backlog bar stays orange, and 'busy workers' creeps up ~1/sec as the pool slowly injects " +
                "threads. Raise 'Min threads' above the burst → the pool has threads ready, so the same " +
                "burst starts at once and the backlog clears. (Non-blocking async wouldn't hold a thread " +
                "at all — no starvation.) Min threads is process-global and is restored when you leave.",
                _tip);

            GUILayout.EndArea();
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
    }
}
