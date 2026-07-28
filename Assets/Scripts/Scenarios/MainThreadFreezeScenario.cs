using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ThreadingLab.Core;
using UnityEngine;

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// Why heavy work must leave the main thread, made visible.
    ///
    /// A "liveness" bar sweeps left-right, advanced from <see cref="Tick"/> — so it only keeps
    /// moving while the main thread is running frames. Run the same deterministic workload on
    /// the main thread and the bar stops dead (FPS -> ~0, the whole app is blocked); run it in
    /// the background and the bar stays smooth while the result is marshaled back on completion.
    ///
    /// Matrix cells: Thread class / running code off the main thread, and marshaling results back.
    /// </summary>
    public sealed class MainThreadFreezeScenario : IThreadingScenario
    {
        public string Title => "2 · Main Thread Freeze";
        public string Description =>
            "The same heavy computation on the main thread vs a background thread. The sweeping " +
            "bar only moves while the main thread runs frames — so a main-thread run freezes it, " +
            "a background run does not.";

        // Deterministic CPU-bound work: count primes <= this. Same input => same result, so the
        // main-thread and background runs are directly comparable. Sized to freeze for a clearly
        // visible ~1-3 s; exact duration varies by CPU, which is acceptable.
        private const int WorkloadLimit = 2_000_000;

        private enum Mode { Main, Background }

        private volatile bool _running;
        private long _lastResult;
        private double _lastMs;
        private float _minFps;
        private Mode _lastMode;
        private bool _hasResult;
        private string _status = "Pick a mode and run.";

        private float _phase;                // liveness-bar animation phase (running-frame seconds)
        private float _worstDeltaDuringRun;  // largest frame delta seen while a run is in progress
        private int _runId;                  // generation guard: a stale callback (post-Exit / re-run) is ignored

        private GUIStyle _big, _mid, _tip;

        public void Enter() { }

        public void Exit()
        {
            // Invalidate any in-flight background callback, stop advancing state, and clear the
            // transient status so a switch-away-and-back never shows enabled buttons + "running…".
            _running = false;
            _runId++;
            _status = "Pick a mode and run.";
        }

        public void Tick(float deltaTime)
        {
            // Only advances while the main thread is running frames — that is the whole demo.
            _phase += deltaTime;
            if (_running) _worstDeltaDuringRun = Mathf.Max(_worstDeltaDuringRun, deltaTime);
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

            DrawLivenessBar();
            GUILayout.Space(14f);

            GUI.enabled = !_running;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run on Main Thread", GUILayout.Height(40), GUILayout.Width(220)))
                RunMainThread();
            if (GUILayout.Button("Run on Background", GUILayout.Height(40), GUILayout.Width(220)))
                RunBackground();
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(16f);
            if (_hasResult)
            {
                GUILayout.Label($"Result:  <b>{_lastResult:n0}</b> primes", _big);
                GUILayout.Label(
                    $"Mode: <b>{_lastMode}</b>   |   Time: <b>{_lastMs:0} ms</b>   |   " +
                    $"min FPS during run: <b>{_minFps:0.#}</b>",
                    _mid);
            }

            GUILayout.Space(8f);
            GUILayout.Label(_running ? "<color=yellow>running…</color>" : _status, _mid);

            GUILayout.Space(16f);
            GUILayout.Label(
                "Watch the sweeping bar and the top-strip FPS. 'Run on Main Thread' stops the bar " +
                "dead and drops FPS to ~0 (the whole app is blocked). 'Run on Background' does the " +
                "same work on a worker thread — the bar keeps sweeping, and the result appears when done.",
                _tip);

            GUILayout.EndArea();
        }

        private void DrawLivenessBar()
        {
            var track = GUILayoutUtility.GetRect(400f, 24f);
            GUI.Box(track, GUIContent.none);

            float t = Mathf.PingPong(_phase * 0.6f, 1f);
            var knob = new Rect(track.x + t * (track.width - 40f), track.y, 40f, track.height);
            var prev = GUI.color;
            GUI.color = new Color(0.4f, 0.8f, 1f);
            GUI.Box(knob, GUIContent.none);
            GUI.color = prev;
        }

        private void RunMainThread()
        {
            if (_running) return;
            BeginRun("running on main thread…");

            var sw = Stopwatch.StartNew();
            long result = Workload(); // BLOCKS the main thread — frames (and Tick) stop here
            sw.Stop();

            // The whole computation was one blocked frame, so its effective FPS is 1000 / ms.
            double ms = sw.Elapsed.TotalMilliseconds;
            float minFps = ms > 0 ? (float)(1000.0 / ms) : 0f;
            Finish(result, ms, Mode.Main, minFps);
        }

        private void RunBackground()
        {
            if (_running) return;
            BeginRun("running on background thread…");
            int myId = _runId;

            // Capture the dispatcher on the MAIN thread. The worker must never call the
            // MainThreadDispatcher.Instance getter — on Editor Stop mid-run it could be null and
            // the getter would create a GameObject from the worker thread (Unity API off-thread).
            var dispatcher = MainThreadDispatcher.Instance;

            Task.Run(() =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    long result = Workload();
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds;

                    // Unity/UI state may only be touched on the main thread.
                    dispatcher.Enqueue(() =>
                    {
                        if (myId != _runId) return; // stale: scenario exited or a newer run started
                        float minFps = _worstDeltaDuringRun > 0f ? 1f / _worstDeltaDuringRun : 0f;
                        Finish(result, ms, Mode.Background, minFps);
                    });
                }
                catch (Exception e)
                {
                    dispatcher.Enqueue(() =>
                    {
                        if (myId != _runId) return;
                        _status = $"<color=red>error: {e.Message}</color>";
                        _running = false;
                    });
                }
            });
        }

        private void BeginRun(string status)
        {
            _running = true;
            _worstDeltaDuringRun = 0f;
            _status = status;
        }

        private void Finish(long result, double ms, Mode mode, float minFps)
        {
            _lastResult = result;
            _lastMs = ms;
            _lastMode = mode;
            _minFps = minFps;
            _hasResult = true;
            _status = "Done.";
            _running = false;
        }

        private static long Workload()
        {
            long count = 0;
            for (int n = 2; n <= WorkloadLimit; n++)
            {
                bool prime = true;
                for (int d = 2; (long)d * d <= n; d++)
                {
                    if (n % d == 0) { prime = false; break; }
                }
                if (prime) count++;
            }
            return count;
        }
    }
}
