using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ThreadingLab.Core
{
    /// <summary>
    /// Marshals work back onto the Unity main thread.
    ///
    /// This is the single most important piece of infrastructure in the whole lab:
    /// worker threads may compute anything, but ANY Unity API call (transforms, meshes,
    /// UI, Debug.Log timing, etc.) must happen on the main thread. Scenarios enqueue a
    /// callback from a background thread; it runs during <see cref="Update"/>.
    ///
    /// Later this same idea becomes a custom SynchronizationContext for the
    /// "role of SynchronizationContext" / custom-awaitable scenarios.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static int _mainThreadId;

        private readonly Queue<Action> _queue = new Queue<Action>();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MainThreadDispatcher]");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>True when called from the Unity main thread.</summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>Queue an action to run on the next main-thread Update.</summary>
        public void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_queue)
            {
                _queue.Enqueue(action);
            }
        }

        private void Update()
        {
            // Drain a snapshot each frame so actions that enqueue more actions
            // don't starve the frame.
            while (true)
            {
                Action next;
                lock (_queue)
                {
                    if (_queue.Count == 0) break;
                    next = _queue.Dequeue();
                }
                try { next(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
