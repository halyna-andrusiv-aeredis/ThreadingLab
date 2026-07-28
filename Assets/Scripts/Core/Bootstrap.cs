using UnityEngine;

namespace ThreadingLab.Core
{
    /// <summary>
    /// Auto-creates the lab host when you press Play, so there is nothing to wire up in a scene.
    /// Open the project, press Play, and the Threading Lab UI appears immediately.
    ///
    /// (If you prefer, you can instead add a <see cref="ThreadingLabHost"/> component to an empty
    /// GameObject in your own scene and delete this file.)
    /// </summary>
    internal static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindFirstObjectByType<ThreadingLabHost>() != null) return;

            var go = new GameObject("[ThreadingLab]");
            go.AddComponent<ThreadingLabHost>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
