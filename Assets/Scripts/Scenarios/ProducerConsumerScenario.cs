using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ThreadingLab.Core;
using UnityEngine;

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// A live bounded producer/consumer, made visible.
    ///
    /// N producer threads push items into a <see cref="ConcurrentQueue{T}"/> that is bounded by two
    /// <see cref="SemaphoreSlim"/> (the classic bounded-buffer): `_emptySlots` (init = capacity) gates
    /// producers, `_fullSlots` (init = 0) gates consumers. When producers outpace consumers the queue
    /// fills and producers block (backpressure); when consumers outpace producers the queue drains and
    /// consumers block (starvation).
    ///
    /// The workers are long-lived, so the discipline here is lifecycle: a CancellationToken stops them
    /// and Stop() joins every thread — no leaks. Changing the worker count or capacity restarts the
    /// set cleanly; changing the rates is applied live (the loops read the fields each iteration).
    ///
    /// Matrix cells: sync via locks/concurrent collections, semaphores (slim), thread coordination.
    /// </summary>
    public sealed class ProducerConsumerScenario : IThreadingScenario
    {
        public string Title => "4 · Producer / Consumer";
        public string Description =>
            "N producers push into a ConcurrentQueue bounded by two SemaphoreSlim; M consumers pull " +
            "and process. Tune the rates to swing between backpressure and starvation.";

        // Structural settings — changing these restarts the worker set.
        private int _producers = 2;
        private int _consumers = 2;
        private int _capacity = 32;
        // Rate settings — applied live (read each loop iteration), no restart needed.
        private int _producerIntervalMs = 40;
        private int _consumerWorkMs = 90;

        private ConcurrentQueue<int> _queue;
        private SemaphoreSlim _emptySlots; // free slots — producers wait on it
        private SemaphoreSlim _fullSlots;  // available items — consumers wait on it
        private CancellationTokenSource _cts;
        private readonly List<Thread> _threads = new List<Thread>();

        private long _produced;
        private long _consumed;
        private int _nextItem;
        private int _blockedProducers; // producers currently waiting on _emptySlots (backpressure)
        private int _idleConsumers;    // consumers currently waiting on _fullSlots (starvation)

        // 1-second rolling throughput.
        private float _sampleTimer;
        private long _lastProduced, _lastConsumed;
        private int _producedPerSec, _consumedPerSec;

        private GUIStyle _big, _mid, _tip;

        public void Enter() => Start();

        public void Exit() => Stop();

        public void Tick(float deltaTime)
        {
            _sampleTimer += deltaTime;
            if (_sampleTimer >= 1f)
            {
                long p = Interlocked.Read(ref _produced), c = Interlocked.Read(ref _consumed);
                _producedPerSec = (int)((p - _lastProduced) / _sampleTimer);
                _consumedPerSec = (int)((c - _lastConsumed) / _sampleTimer);
                _lastProduced = p;
                _lastConsumed = c;
                _sampleTimer = 0f;
            }
        }

        private void Start()
        {
            _queue = new ConcurrentQueue<int>();
            _emptySlots = new SemaphoreSlim(_capacity, _capacity);
            _fullSlots = new SemaphoreSlim(0, _capacity);
            _cts = new CancellationTokenSource();
            _produced = _consumed = 0;
            _nextItem = 0;
            _blockedProducers = _idleConsumers = 0;
            _lastProduced = _lastConsumed = 0;
            _producedPerSec = _consumedPerSec = 0;
            _sampleTimer = 0f;

            // Bind each worker to THIS generation's queue + semaphores (like ct). If a worker ever
            // outlives Stop(), it touches its own disposed objects (caught) — never the next set's.
            var queue = _queue;
            var empty = _emptySlots;
            var full = _fullSlots;
            var ct = _cts.Token;
            for (int i = 0; i < _producers; i++)
            {
                var t = new Thread(() => ProducerLoop(queue, empty, full, ct)) { IsBackground = true, Name = $"PC-Producer-{i}" };
                _threads.Add(t);
                t.Start();
            }
            for (int i = 0; i < _consumers; i++)
            {
                var t = new Thread(() => ConsumerLoop(queue, empty, full, ct)) { IsBackground = true, Name = $"PC-Consumer-{i}" };
                _threads.Add(t);
                t.Start();
            }
        }

        private void Stop()
        {
            _cts?.Cancel();
            bool allStopped = true;
            foreach (var t in _threads) if (!t.Join(2000)) allStopped = false; // token releases blocked Waits
            _threads.Clear();
            if (!allStopped) Debug.LogWarning("ProducerConsumer: a worker did not stop within the join timeout.");

            _cts?.Dispose(); _cts = null;
            _emptySlots?.Dispose(); _emptySlots = null;
            _fullSlots?.Dispose(); _fullSlots = null;
            _queue = null;
        }

        // Cancel + join the current set, then start a fresh one. Called on the main thread when a
        // structural setting (producer/consumer count, capacity) changes. NOTE: Stop() joins on the
        // main thread, so a structural change costs a brief (~one sleep interval) frame stall — a
        // deliberate trade-off for a clean restart.
        private void Restart()
        {
            Stop();
            Start();
        }

        private void ProducerLoop(ConcurrentQueue<int> queue, SemaphoreSlim empty, SemaphoreSlim full, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Interlocked.Increment(ref _blockedProducers);
                    try { empty.Wait(ct); }                      // block here if the queue is full (backpressure)
                    finally { Interlocked.Decrement(ref _blockedProducers); }

                    queue.Enqueue(Interlocked.Increment(ref _nextItem));
                    full.Release();                              // signal one item available
                    Interlocked.Increment(ref _produced);
                    Thread.Sleep(_producerIntervalMs);
                }
            }
            catch (OperationCanceledException) { }               // normal shutdown
            catch (ObjectDisposedException) { }                  // raced teardown — safe to ignore
        }

        private void ConsumerLoop(ConcurrentQueue<int> queue, SemaphoreSlim empty, SemaphoreSlim full, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Interlocked.Increment(ref _idleConsumers);
                    try { full.Wait(ct); }                       // block here if the queue is empty (starvation)
                    finally { Interlocked.Decrement(ref _idleConsumers); }

                    queue.TryDequeue(out _);
                    empty.Release();                             // signal one free slot
                    Interlocked.Increment(ref _consumed);
                    Thread.Sleep(_consumerWorkMs);               // simulate processing the item
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
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

            // --- controls: structural (restart on change) ---
            int np = Stepper("Producers", _producers, 1, 6, 1);
            int nc = Stepper("Consumers", _consumers, 1, 6, 1);
            int cap = Stepper("Capacity", _capacity, 4, 128, 4);
            if (np != _producers || nc != _consumers || cap != _capacity)
            {
                _producers = np;
                _consumers = nc;
                _capacity = cap;
                Restart();
            }
            // --- controls: rates (live, no restart) ---
            _producerIntervalMs = Stepper("Producer ms", _producerIntervalMs, 0, 500, 10);
            _consumerWorkMs = Stepper("Consumer ms", _consumerWorkMs, 0, 500, 10);

            GUILayout.Space(10f);

            int depth = _queue?.Count ?? 0;
            GUILayout.Label($"Queue depth: <b>{depth}</b> / {_capacity}", _big);

            var bar = GUILayoutUtility.GetRect(320f, 24f, GUILayout.ExpandWidth(false));
            GUI.Box(bar, GUIContent.none);
            float frac = _capacity > 0 ? Mathf.Clamp01(depth / (float)_capacity) : 0f;
            var prev = GUI.color;
            GUI.color = new Color(0.4f, 0.8f, 1f);
            GUI.Box(new Rect(bar.x, bar.y, bar.width * frac, bar.height), GUIContent.none);
            GUI.color = prev;

            GUILayout.Space(10f);

            int blocked = _blockedProducers, idle = _idleConsumers;
            GUILayout.Label(
                $"Backpressure: <color={(blocked > 0 ? "orange" : "white")}>{blocked}</color> producers blocked   |   " +
                $"Starvation: <color={(idle > 0 ? "yellow" : "white")}>{idle}</color> consumers idle",
                _mid);
            GUILayout.Label(
                $"Produced: <b>{Interlocked.Read(ref _produced)}</b> ({_producedPerSec}/s)   |   " +
                $"Consumed: <b>{Interlocked.Read(ref _consumed)}</b> ({_consumedPerSec}/s)",
                _mid);

            GUILayout.Space(14f);
            GUILayout.Label(
                "Two SemaphoreSlim bound the ConcurrentQueue: producers wait for a free slot, consumers " +
                "wait for an item. Make consumers slower/fewer → the queue pins at capacity and producers " +
                "block (backpressure, no unbounded growth). Make them faster/more → the queue empties and " +
                "consumers sit idle (starvation).",
                _tip);

            GUILayout.EndArea();
        }

        private int Stepper(string label, int value, int min, int max, int step)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(240f));
            GUILayout.Label(label, _mid, GUILayout.Width(96f));
            if (GUILayout.Button("-", GUILayout.Width(28f))) value = Mathf.Max(min, value - step);
            GUILayout.Label(value.ToString(), _mid, GUILayout.Width(48f));
            if (GUILayout.Button("+", GUILayout.Width(28f))) value = Mathf.Min(max, value + step);
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
