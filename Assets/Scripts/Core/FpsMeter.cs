namespace ThreadingLab.Core
{
    /// <summary>
    /// Rolling FPS / frame-time meter. The whole point of the lab is to SEE the main thread
    /// freeze (FPS -> 0) when heavy work runs on it, and stay smooth when it runs off-thread,
    /// so this feeds the always-visible metrics strip.
    /// </summary>
    public sealed class FpsMeter
    {
        private const int SampleCount = 30;
        private readonly float[] _samples = new float[SampleCount];
        private int _index;
        private int _filled;

        public float CurrentFps { get; private set; }
        public float MinFps { get; private set; } = float.MaxValue;

        public void Sample(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f) return;

            _samples[_index] = unscaledDeltaTime;
            _index = (_index + 1) % SampleCount;
            if (_filled < SampleCount) _filled++;

            float sum = 0f;
            for (int i = 0; i < _filled; i++) sum += _samples[i];
            float avgDelta = sum / _filled;

            CurrentFps = avgDelta > 0f ? 1f / avgDelta : 0f;
            if (CurrentFps < MinFps) MinFps = CurrentFps;
        }

        public void ResetMin() => MinFps = float.MaxValue;
    }
}
