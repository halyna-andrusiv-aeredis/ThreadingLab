using System;
using System.Collections.Generic;
using System.Threading;
using ThreadingLab.Core;
using UnityEngine;

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// Two synchronization lessons in one scenario:
    ///
    /// 1. Deadlock — two threads take two locks in OPPOSITE order (A: lock1→lock2, B: lock2→lock1),
    ///    each holding its first briefly before requesting the second, so they block each other. The
    ///    "Fix (ordered locks)" toggle makes both acquire in the SAME order, which cannot deadlock.
    ///    Teardown-safe: the second lock is taken with Monitor.TryEnter in a token-checked loop, so a
    ///    "deadlocked" thread makes no progress yet still exits promptly on cancel.
    ///
    /// 2. ReaderWriterLock — 4 reader threads + 1 writer over a shared value. Under
    ///    ReaderWriterLockSlim readers run CONCURRENTLY (high reads/sec); toggling to a single lock
    ///    serializes them (reads/sec drops). Each read does a small fixed amount of work so the
    ///    overlap actually matters.
    ///
    /// Matrix cells: monitors, ReadWriteLocks (+ slim), and deadlock (lock ordering) with its fix.
    /// </summary>
    public sealed class SyncPrimitivesDeadlockScenario : IThreadingScenario
    {
        public string Title => "6 · Deadlock & ReaderWriterLock";
        public string Description =>
            "Two threads deadlock by taking locks in opposite order (flip the fix to order them); and " +
            "readers run concurrently under ReaderWriterLockSlim but serialize under a plain lock.";

        // --- deadlock part ---
        private const int HoldMs = 30;      // hold the first lock before requesting the second
        private const int PollMs = 25;      // TryEnter poll (keeps the token responsive)
        private const int WorkMs = 5;
        private const int DeadlockMs = 400; // both waiting longer than this -> flag DEADLOCK

        private readonly object _lockA = new object();
        private readonly object _lockB = new object();
        private volatile bool _ordered;     // the Fix toggle (needs Restart to break an active deadlock)

        private long _progress;
        private readonly int[] _waitingSince = new int[2];
        private readonly string[] _state = { "idle", "idle" };

        // --- reader/writer part ---
        private const int Readers = 4;
        private const int WriterDelayMs = 40;
        private const int ReadSpin = 250;   // fixed work done while holding the read lock

        private ReaderWriterLockSlim _rw;
        private readonly object _plainLock = new object();
        // LIVE toggle (no restart): readers/writer read it each iteration, so reads/sec changes
        // instantly (that instant A/B is the point). The brief switch window where some threads still
        // use the old lock is intentional and benign — _sharedValue is an aligned int, and the reader
        // only copies it into a discarded local, so no torn value, crash, or disposed-lock access.
        private volatile bool _useRwLock = true;
        private int _sharedValue;

        private long _reads, _writes;
        private float _sampleTimer;
        private long _lastReads, _lastWrites;
        private int _readsPerSec, _writesPerSec;

        // --- lifecycle ---
        private CancellationTokenSource _cts;
        private readonly List<Thread> _threads = new List<Thread>();

        private GUIStyle _big, _mid, _tip;

        public void Enter() => Start();

        public void Exit() => Stop();

        public void Tick(float deltaTime)
        {
            _sampleTimer += deltaTime;
            if (_sampleTimer >= 1f)
            {
                long r = Interlocked.Read(ref _reads), w = Interlocked.Read(ref _writes);
                _readsPerSec = (int)((r - _lastReads) / _sampleTimer);
                _writesPerSec = (int)((w - _lastWrites) / _sampleTimer);
                _lastReads = r;
                _lastWrites = w;
                _sampleTimer = 0f;
            }
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();
            _progress = 0;
            _waitingSince[0] = _waitingSince[1] = 0;
            _state[0] = _state[1] = "idle";
            _reads = _writes = 0;
            _lastReads = _lastWrites = 0;
            _readsPerSec = _writesPerSec = 0;
            _sampleTimer = 0f;
            _rw = new ReaderWriterLockSlim();

            var ct = _cts.Token;
            var rw = _rw; // bind this generation's lock into the worker closures

            for (int i = 0; i < 2; i++)
            {
                int index = i;
                Add(new Thread(() => DeadlockThread(index, ct)) { IsBackground = true, Name = $"DL-{index}" });
            }
            for (int i = 0; i < Readers; i++)
            {
                Add(new Thread(() => ReaderLoop(ct, rw)) { IsBackground = true, Name = $"RW-Reader-{i}" });
            }
            Add(new Thread(() => WriterLoop(ct, rw)) { IsBackground = true, Name = "RW-Writer" });

            void Add(Thread t) { _threads.Add(t); t.Start(); }
        }

        private void Stop()
        {
            _cts?.Cancel();
            foreach (var t in _threads) t.Join(500); // workers poll the token every ~25 ms -> exit fast
            _threads.Clear();
            _cts?.Dispose(); _cts = null;
            _rw?.Dispose(); _rw = null; // safe: all workers joined first
        }

        private void Restart() { Stop(); Start(); }

        // --- deadlock workers ---

        private void DeadlockThread(int index, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool aFirst = _ordered || index == 0;
                    object first = aFirst ? _lockA : _lockB;
                    object second = aFirst ? _lockB : _lockA;
                    string fn = aFirst ? "A" : "B";
                    string sn = aFirst ? "B" : "A";

                    _state[index] = $"waiting {fn}";
                    bool gotFirst = false;
                    while (!ct.IsCancellationRequested && !(gotFirst = Monitor.TryEnter(first, PollMs))) { }
                    if (!gotFirst) break;

                    try
                    {
                        _state[index] = $"holds {fn}, wants {sn}";
                        Thread.Sleep(HoldMs);

                        _waitingSince[index] = Environment.TickCount;
                        bool gotSecond = false;
                        while (!ct.IsCancellationRequested && !(gotSecond = Monitor.TryEnter(second, PollMs))) { }
                        _waitingSince[index] = 0;
                        if (!gotSecond) break;

                        try
                        {
                            _state[index] = $"holds {fn}+{sn}";
                            Interlocked.Increment(ref _progress);
                            Thread.Sleep(WorkMs);
                        }
                        finally { Monitor.Exit(second); }
                    }
                    finally { Monitor.Exit(first); }

                    _state[index] = "idle";
                }
            }
            catch (Exception) { }
            finally
            {
                _waitingSince[index] = 0;
                _state[index] = "stopped";
            }
        }

        // --- reader/writer workers ---

        private void ReaderLoop(CancellationToken ct, ReaderWriterLockSlim rw)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_useRwLock)
                    {
                        rw.EnterReadLock();
                        try { ReadWork(); } finally { rw.ExitReadLock(); }
                    }
                    else
                    {
                        Monitor.Enter(_plainLock);
                        try { ReadWork(); } finally { Monitor.Exit(_plainLock); }
                    }
                    Interlocked.Increment(ref _reads);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        private void WriterLoop(CancellationToken ct, ReaderWriterLockSlim rw)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_useRwLock)
                    {
                        rw.EnterWriteLock();
                        try { _sharedValue++; } finally { rw.ExitWriteLock(); }
                    }
                    else
                    {
                        Monitor.Enter(_plainLock);
                        try { _sharedValue++; } finally { Monitor.Exit(_plainLock); }
                    }
                    Interlocked.Increment(ref _writes);
                    Thread.Sleep(WriterDelayMs);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        // A small fixed read cost so concurrent readers (RWLock) beat serialized ones (plain lock).
        private void ReadWork()
        {
            int _ = _sharedValue;
            Thread.SpinWait(ReadSpin);
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

            // ---- Deadlock ----
            bool ordered = GUILayout.Toggle(_ordered, "  Fix (ordered locks) — both take A then B");
            if (ordered != _ordered) { _ordered = ordered; Restart(); }

            GUILayout.Space(8f);
            GUILayout.Label("Deadlock (two threads, two locks)", _big);

            int now = Environment.TickCount;
            int w0 = _waitingSince[0], w1 = _waitingSince[1];
            bool deadlock = w0 != 0 && w1 != 0 && (now - w0 > DeadlockMs) && (now - w1 > DeadlockMs);

            GUILayout.Label($"Thread A: <b>{_state[0]}</b>   |   Thread B: <b>{_state[1]}</b>", _mid);
            GUILayout.Label(deadlock
                ? "<color=red><b>DEADLOCK</b></color> — each holds one lock, waits for the other"
                : "<color=lime>running</color>", _mid);
            GUILayout.Label($"Progress (both-locks acquisitions): <b>{Interlocked.Read(ref _progress)}</b>", _mid);

            GUILayout.Space(16f);

            // ---- ReaderWriterLock ----
            _useRwLock = GUILayout.Toggle(_useRwLock, "  Use ReaderWriterLockSlim (off = one plain lock)");
            GUILayout.Label("Readers vs writer (4 readers, 1 writer)", _big);
            GUILayout.Label($"reads/sec: <b>{_readsPerSec}</b>   |   writes/sec: <b>{_writesPerSec}</b>", _mid);

            GUILayout.Space(14f);
            GUILayout.Label(
                "Deadlock: unordered, each thread grabs one lock and waits forever for the other — " +
                "Progress freezes; the fix orders the locks so one always finishes.  " +
                "RWLock: readers overlap so reads/sec is high; switch to a plain lock and they " +
                "serialize — reads/sec drops sharply.",
                _tip);

            GUILayout.EndArea();
        }
    }
}
