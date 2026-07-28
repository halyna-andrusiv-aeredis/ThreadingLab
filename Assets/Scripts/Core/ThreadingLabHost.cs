using System.Collections.Generic;
using ThreadingLab.Scenarios;
using UnityEngine;

namespace ThreadingLab.Core
{
    /// <summary>
    /// The shell. Drop this one component on an empty GameObject and press Play — it builds its
    /// own UI in <see cref="OnGUI"/>, so there is nothing to wire up in the scene.
    ///
    /// Left: scenario picker. Right: the active scenario's controls + visualization.
    /// Top strip: always-on metrics (FPS / min FPS / thread count) so you can watch the main
    /// thread react in real time.
    ///
    /// To add a demo: implement <see cref="IThreadingScenario"/> and add it to <see cref="BuildScenarios"/>.
    /// </summary>
    public sealed class ThreadingLabHost : MonoBehaviour
    {
        private readonly List<IThreadingScenario> _scenarios = new List<IThreadingScenario>();
        private readonly FpsMeter _fps = new FpsMeter();

        private int _activeIndex = -1;
        private IThreadingScenario _active;

        private GUIStyle _titleStyle;
        private GUIStyle _descStyle;
        private GUIStyle _stripStyle;
        private GUIStyle _pickerHeaderStyle;
        private GUIStyle _pickerButtonStyle;
        private Vector2 _pickerScroll;

        // Cached process-thread count. GetCurrentProcess().Threads enumerates the OS thread
        // list — far too heavy to call every OnGUI event (and it would perturb the very FPS
        // metric this strip displays), so refresh it on a slow timer instead.
        private int _threadCount;
        private float _nextThreadPoll;

        private const float TopStripHeight = 40f;
        private const float PickerWidth = 220f;
        private const float ThreadPollInterval = 0.5f;

        private void Awake()
        {
            // Ensure the dispatcher exists from the start (main-thread id captured now).
            _ = MainThreadDispatcher.Instance;
            BuildScenarios();
            SetActive(0);
        }

        private void BuildScenarios()
        {
            _scenarios.Clear();
            _scenarios.Add(new RaceConditionScenario());
            _scenarios.Add(new MainThreadFreezeScenario());
            _scenarios.Add(new WaysToRunInParallelScenario());
            _scenarios.Add(new ProducerConsumerScenario());
            _scenarios.Add(new ThreadPoolStarvationScenario());
            // Next demos plug in here:
            // _scenarios.Add(new DeadlockScenario());
        }

        private void Update()
        {
            _fps.Sample(Time.unscaledDeltaTime);
            _active?.Tick(Time.deltaTime);

            if (Time.unscaledTime >= _nextThreadPoll)
            {
                _nextThreadPoll = Time.unscaledTime + ThreadPollInterval;
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                _threadCount = proc.Threads.Count;
            }
        }

        private void OnDestroy()
        {
            _active?.Exit();
        }

        private void SetActive(int index)
        {
            if (index < 0 || index >= _scenarios.Count) return;
            if (index == _activeIndex) return;

            _active?.Exit();
            _activeIndex = index;
            _active = _scenarios[index];
            _fps.ResetMin();
            _active.Enter();
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawTopStrip();
            DrawPicker();

            if (_active != null)
            {
                var area = new Rect(
                    PickerWidth + 10f,
                    TopStripHeight + 10f,
                    Screen.width - PickerWidth - 20f,
                    Screen.height - TopStripHeight - 20f);

                GUILayout.BeginArea(area, GUI.skin.box);
                GUILayout.Label(_active.Title, _titleStyle);
                GUILayout.Label(_active.Description, _descStyle);
                GUILayout.Space(6f);
                GUILayout.EndArea();

                var content = new Rect(area.x + 8f, area.y + 64f, area.width - 16f, area.height - 72f);
                _active.DrawGUI(content);
            }
        }

        private void DrawTopStrip()
        {
            var rect = new Rect(0, 0, Screen.width, TopStripHeight);
            GUI.Box(rect, GUIContent.none);

            string fpsColor = _fps.CurrentFps < 15f ? "red" : _fps.CurrentFps < 45f ? "yellow" : "lime";

            var label =
                $"<b>Threading Lab</b>   |   " +
                $"FPS <color={fpsColor}>{_fps.CurrentFps:0}</color>   " +
                $"min <color=orange>{(_fps.MinFps == float.MaxValue ? 0 : _fps.MinFps):0}</color>   |   " +
                $"process threads: {_threadCount}   |   " +
                $"cores: {SystemInfo.processorCount}";

            GUI.Label(rect, label, _stripStyle);
        }

        private void DrawPicker()
        {
            var rect = new Rect(0, TopStripHeight, PickerWidth, Screen.height - TopStripHeight);
            GUI.Box(rect, GUIContent.none);

            GUILayout.BeginArea(new Rect(8, TopStripHeight + 8, PickerWidth - 16, Screen.height - TopStripHeight - 16));
            GUILayout.Label("<b>Scenarios</b>", _pickerHeaderStyle);
            GUILayout.Space(6f);

            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
            for (int i = 0; i < _scenarios.Count; i++)
            {
                bool selected = i == _activeIndex;
                if (selected) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button(_scenarios[i].Title, _pickerButtonStyle, GUILayout.Height(32)))
                    SetActive(i);
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return; // build all styles once — no per-frame GUIStyle allocations

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            _stripStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, padding = new RectOffset(12, 0, 10, 0) };
            _pickerHeaderStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
            _pickerButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
        }
    }
}
