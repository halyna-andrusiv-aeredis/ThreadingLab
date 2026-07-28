using UnityEngine;

namespace ThreadingLab.Core
{
    /// <summary>
    /// A single self-contained threading demo (Race Condition, Deadlock, ThreadPool, ...).
    /// The host (<see cref="ThreadingLabHost"/>) owns the list of scenarios, shows a picker,
    /// and drives the selected one. Every visualization in the lab is just a new implementation
    /// of this interface — start with one, grow to many, without touching the shell.
    /// </summary>
    public interface IThreadingScenario
    {
        /// <summary>Short name shown in the scenario picker.</summary>
        string Title { get; }

        /// <summary>One-line description shown at the top of the scenario area.</summary>
        string Description { get; }

        /// <summary>Called once when this scenario becomes the active one.</summary>
        void Enter();

        /// <summary>Called once when leaving this scenario — MUST stop threads / release resources.</summary>
        void Exit();

        /// <summary>Per-frame update on the Unity main thread. Pull results from worker threads here.</summary>
        void Tick(float deltaTime);

        /// <summary>Draw controls and visualization with IMGUI inside the given screen area.</summary>
        void DrawGUI(Rect area);
    }
}
