using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ThreadingLab.Core;
using UnityEngine;

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// The classic race condition, made visible.
    ///
    /// N worker threads each increment a shared counter M times. Expected result is always N*M.
    /// - No sync   -> "counter++" is read-modify-write; updates are lost, result &lt; expected, and
    ///                DIFFERENT every run (non-determinism you can see).
    /// - lock      -> correct result, slowest.
    /// - Interlocked -> correct result, much faster than lock (atomic hardware instruction).
    ///
    /// Matrix cells demonstrated: Thread synchronization primitives (lock/Monitor), atomic
    /// operations (Interlocked), and why "counter++" is not atomic.
    /// </summary>
    public sealed class RaceConditionScenario : IThreadingScenario
    {
        public string Title => "1 · Race Condition";
        public string Description =>
            "8 threads × 100 000 increments on one shared counter. Expected = 800 000. " +
            "Watch how 'No sync' loses updates and gives a different wrong number every run.";

        private const int Threads = 8;
        private const int IncrementsPerThread = 100_000;
        private const long Expected = (long)Threads * IncrementsPerThread;

        private enum Mode { None, Lock, Interlocked }

        // Shared mutable state touched by all worker threads.
        private long _counter;
        private readonly object _gate = new object();

        // UI/result state — only written back on the main thread via the dispatcher.
        private volatile bool _running;
        private long _lastResult;
        private double _lastMs;
        private Mode _lastMode;
        private string _status = "Pick a mode and run.";

        public void Enter() { }

        public void Exit()
        {
            // Nothing long-lived: runs complete quickly. Flag guards against a late callback.
            _running = false;
        }

        public void Tick(float deltaTime) { }

        // Cached IMGUI styles — built once, never per-frame (see project rule on DrawGUI allocations).
        private GUIStyle _big, _mid, _tip;

        private void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _tip = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
        }

        public void DrawGUI(Rect area)
        {
            EnsureStyles();
            GUILayout.BeginArea(area);

            GUI.enabled = !_running;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run: No sync (counter++)", GUILayout.Height(40))) Run(Mode.None);
            if (GUILayout.Button("Run: lock", GUILayout.Height(40))) Run(Mode.Lock);
            if (GUILayout.Button("Run: Interlocked", GUILayout.Height(40))) Run(Mode.Interlocked);
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(16f);

            GUILayout.Label($"Expected:  <b>{Expected:n0}</b>", _big);

            bool correct = _lastResult == Expected;
            string color = _lastResult == 0 ? "white" : correct ? "lime" : "red";
            GUILayout.Label($"Actual:      <color={color}><b>{_lastResult:n0}</b></color>", _big);

            if (_lastResult != 0)
            {
                long lost = Expected - _lastResult;
                GUILayout.Label(
                    $"Mode: <b>{_lastMode}</b>   |   Time: <b>{_lastMs:0.0} ms</b>   |   " +
                    (correct ? "<color=lime>correct</color>" : $"<color=red>lost {lost:n0} updates</color>"),
                    _mid);
            }

            GUILayout.Space(10f);
            GUILayout.Label(_running ? "<color=yellow>running…</color>" : _status, _mid);

            GUILayout.Space(20f);
            GUILayout.Label(
                "Tip: run 'No sync' several times — the number changes each time. That non-determinism " +
                "is exactly what makes race conditions so nasty to debug.",
                _tip);

            GUILayout.EndArea();
        }

        private void Run(Mode mode)
        {
            if (_running) return;
            _running = true;
            _status = "running…";
            _counter = 0;

            // Do the threading work OFF the main thread so Unity stays responsive,
            // then marshal the result back via the dispatcher.
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var workers = new Thread[Threads];
                for (int i = 0; i < Threads; i++)
                {
                    workers[i] = new Thread(() => Work(mode));
                    workers[i].Start();
                }
                for (int i = 0; i < Threads; i++) workers[i].Join();
                sw.Stop();

                long result = Interlocked.Read(ref _counter);
                double ms = sw.Elapsed.TotalMilliseconds;

                MainThreadDispatcher.Instance.Enqueue(() =>
                {
                    _lastResult = result;
                    _lastMs = ms;
                    _lastMode = mode;
                    _status = "Done.";
                    _running = false;
                });
            });
        }

        private void Work(Mode mode)
        {
            for (int i = 0; i < IncrementsPerThread; i++)
            {
                switch (mode)
                {
                    case Mode.None:
                        _counter++; // read-modify-write: NOT atomic -> race
                        break;
                    case Mode.Lock:
                        lock (_gate) { _counter++; }
                        break;
                    case Mode.Interlocked:
                        Interlocked.Increment(ref _counter);
                        break;
                }
            }
        }
    }
}
