using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ThreadingLab.Core;
using UnityEngine;
using MathF = System.MathF; // native float trig — much faster than Unity's Mathf (which round-trips through double)

namespace ThreadingLab.Scenarios
{
    /// <summary>
    /// The different ways to run the same parallel workload, made visual — with an authentic
    /// particle "creature" (ported from @yuruyurau's Processing sketch).
    ///
    /// Every frame, N points are mapped through a trig function to screen positions and *splatted*
    /// (additively accumulated) into a brightness buffer, which is mapped to a red-orange texture.
    /// The creature is the accumulated point cloud. The same per-frame work runs three ways, live:
    /// Sequential / Parallel.For / ThreadPool.
    ///
    /// A scatter workload is NOT embarrassingly parallel — two workers could splat the same pixel.
    /// We keep it race-free the idiomatic way: each worker accumulates into its OWN buffer, and the
    /// main thread merges them (addition is commutative → deterministic). Merge + upload are
    /// main-thread only. Matrix cell: different ways to run code in parallel + the downside of each.
    /// </summary>
    public sealed class WaysToRunInParallelScenario : IThreadingScenario
    {
        public string Title => "3 · Ways to Run in Parallel";
        public string Description =>
            "An authentic particle 'creature' (millions of sin/cos points) splatted every frame. " +
            "Same work, different execution — switch method / point-count and watch compute-ms and FPS.";

        private const int W = 400;
        private const int H = 400;
        private const float ParamRange = 10000f; // the source samples the curve over i in [0, 1e4]
        private const float AnimSpeed = 12f;      // the source advances t by ~PI/15 per frame; scale real time to match

        private static readonly int[] PointSteps = { 250_000, 1_000_000, 2_000_000, 4_000_000 };

        private enum Method { Sequential, ParallelFor, ThreadPool }

        private int _points = 250_000;
        private Method _method = Method.Sequential;
        private int _workers;

        private Texture2D _tex;
        private Color32[] _pixels;
        private int[] _accum;        // merged accumulator (also the Sequential target)
        private int[][] _threadAccum; // one accumulation buffer per worker (parallel methods)
        private CountdownEvent _countdown; // reused across frames (no per-frame alloc in the timed path)

        private float _time;
        private double _computeMs;
        private float _frameMs; // total per-frame time (responsive) — the honest FPS source

        // One-time auto-fit transform (bbox of a subsample at t=0, scaled to ~80% of the canvas).
        private float _fitScale = 1f, _cx, _cy;

        // Per-point cache of the TIME-INDEPENDENT parts of the @yuruyurau formula (they depend only
        // on the point index, not on t). Rebuilt when the point count changes; lets the per-frame
        // hot path do 3 trig ops/point instead of 8.
        private float[] _cQBase; // 3*sin(atan2(k,e)*19)
        private float[] _cA;     // sin(y/19) * k
        private float[] _cD;     // d = mag(k,e)
        private float[] _cE9;    // e * 9
        private int _cachedFor = -1;

        private GUIStyle _mid, _tip;

        public void Enter()
        {
            _workers = Mathf.Max(1, System.Environment.ProcessorCount);
            _pixels = new Color32[W * H];
            _accum = new int[W * H];
            _threadAccum = new int[_workers][];
            for (int i = 0; i < _workers; i++) _threadAccum[i] = new int[W * H];
            _countdown = new CountdownEvent(_workers);

            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            ComputeFit();
            RebuildCache();
            // Prime one frame so a freshly-created texture is never drawn uninitialized.
            RenderFrame(Method.Sequential, _points, _time);
        }

        public void Exit()
        {
            if (_tex != null) Object.Destroy(_tex);
            _tex = null;
            _pixels = null;
            _accum = null;
            _threadAccum = null;
            _countdown?.Dispose();
            _countdown = null;
            _cQBase = _cA = _cD = _cE9 = null;
            _cachedFor = -1;
        }

        public void Tick(float deltaTime)
        {
            if (_tex == null) return;

            _time += deltaTime * AnimSpeed;
            if (deltaTime > 0f) _frameMs = Mathf.Lerp(_frameMs, deltaTime * 1000f, 0.25f);

            if (_cachedFor != _points) RebuildCache(); // point-count changed via the weight control
            RenderFrame(_method, _points, _time);
        }

        private void RenderFrame(Method method, int points, float time)
        {
            var sw = Stopwatch.StartNew();
            Accumulate(method, points, time);
            sw.Stop();
            _computeMs = sw.Elapsed.TotalMilliseconds;

            Present(method != Method.Sequential); // parallel merge + colorize, then upload
        }

        private void EnsureStyles()
        {
            if (_mid != null) return; // build once — no per-frame GUIStyle allocations
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _tip = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
        }

        public void DrawGUI(Rect area)
        {
            EnsureStyles();
            GUILayout.BeginArea(area);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Method:", _mid, GUILayout.Width(64));
            MethodButton(Method.Sequential, "Sequential");
            MethodButton(Method.ParallelFor, "Parallel.For");
            MethodButton(Method.ThreadPool, "ThreadPool");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Points:", _mid, GUILayout.Width(64));
            foreach (int p in PointSteps)
            {
                if (_points == p) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button(PointLabel(p), GUILayout.Height(28), GUILayout.Width(70))) _points = p;
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label(
                $"Compute: <b>{_computeMs:0.0} ms</b> (parallelizable)   |   Frame: <b>{_frameMs:0} ms</b>   |   " +
                $"FPS: <b>{(_frameMs > 0f ? 1000f / _frameMs : 0f):0}</b>   |   " +
                $"{PointLabel(_points)} pts   |   cores: {SystemInfo.processorCount}",
                _mid);
            GUILayout.Space(8f);

            float size = Mathf.Min(area.width - 16f, area.height - 170f);
            if (size > 32f && _tex != null)
            {
                var texRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(texRect, _tex, ScaleMode.ScaleToFit);
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                "A particle cloud (@yuruyurau). Raise Points until Sequential drops the FPS, then " +
                "switch to Parallel.For / ThreadPool. A scatter workload needs per-thread buffers + " +
                "a merge — not every parallel job is free.",
                _tip);

            GUILayout.EndArea();
        }

        private void MethodButton(Method m, string label)
        {
            if (_method == m) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button(label, GUILayout.Height(28))) _method = m;
            GUI.backgroundColor = Color.white;
        }

        private static string PointLabel(int p) => p >= 1_000_000 ? $"{p / 1_000_000f:0.#}M" : $"{p / 1000}k";

        // --- accumulation -------------------------------------------------------------------

        private void Accumulate(Method method, int n, float time)
        {
            if (method == Method.Sequential)
            {
                System.Array.Clear(_accum, 0, _accum.Length);
                PlotRange(0, n, time, _accum);
                return;
            }

            int p = _workers;
            int per = Mathf.CeilToInt(n / (float)p);
            for (int i = 0; i < p; i++) System.Array.Clear(_threadAccum[i], 0, _threadAccum[i].Length);

            if (method == Method.ParallelFor)
            {
                // TPL: one task per partition, each into its OWN buffer (no shared write).
                Parallel.For(0, p, idx =>
                {
                    int start = idx * per;
                    int end = Mathf.Min(n, start + per);
                    PlotRange(start, end, time, _threadAccum[idx]);
                });
            }
            else // ThreadPool
            {
                _countdown.Reset(p); // reuse the cached event instead of allocating one per frame
                for (int idx = 0; idx < p; idx++)
                {
                    int captured = idx;
                    int start = idx * per;
                    int end = Mathf.Min(n, start + per);
                    bool queued = ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { PlotRange(start, end, time, _threadAccum[captured]); }
                        finally { _countdown.Signal(); }
                    });
                    if (!queued) _countdown.Signal(); // queue refused — don't let Wait() hang
                }
                _countdown.Wait();
            }
            // Merge is done in Present() — parallelized over pixel ranges, not serially here.
        }

        // Splat one contiguous range of points into an accumulation buffer. Uses the per-point cache,
        // so only the 3 time-dependent trig ops are evaluated per frame (not the full 8).
        private void PlotRange(int start, int end, float time, int[] buf)
        {
            float cScale = _fitScale;
            float halfW = W * 0.5f, halfH = H * 0.5f;
            for (int idx = start; idx < end; idx++)
            {
                float d = _cD[idx];
                float s = MathF.Sin(_cE9[idx] - d * 3f + time * 0.25f);
                float q = _cQBase[idx] + _cA[idx] * (9f + 2f * s);
                float c = d - time * 0.125f;
                float px = q + 60f * MathF.Cos(c) + 200f;
                float py = q * MathF.Sin(c) + d * 39f - 195f;
                int ix = (int)((px - _cx) * cScale + halfW);
                int iy = (int)((py - _cy) * cScale + halfH);
                if ((uint)ix < (uint)W && (uint)iy < (uint)H) buf[iy * W + ix]++;
            }
        }

        // Precompute the time-independent parts of the @yuruyurau formula for every point. Heavy but
        // one-time (only when the point count changes); parallelized to keep the hitch small.
        private void RebuildCache()
        {
            int n = _points;
            if (_cQBase == null || _cQBase.Length != n)
            {
                _cQBase = new float[n];
                _cA = new float[n];
                _cD = new float[n];
                _cE9 = new float[n];
            }
            float step = ParamRange / n;
            Parallel.For(0, n, idx =>
            {
                float i = idx * step;
                float y = i / 235f;
                float k = 4f * MathF.Cos(i / 29f);
                float e = y / 7f - 13f;
                _cD[idx] = MathF.Sqrt(k * k + e * e);
                _cQBase[idx] = 3f * MathF.Sin(MathF.Atan2(k, e) * 19f);
                _cA[idx] = MathF.Sin(y / 19f) * k;
                _cE9[idx] = e * 9f;
            });
            _cachedFor = n;
        }

        // Full @yuruyurau point function (index i -> px, py) — used only by ComputeFit (one-time).
        private static void MapPoint(float i, float t, out float px, out float py)
        {
            float x = i;
            float y = i / 235f;
            float k = 4f * MathF.Cos(x / 29f);
            float e = y / 7f - 13f;
            float d = MathF.Sqrt(k * k + e * e);
            float q = 3f * MathF.Sin(MathF.Atan2(k, e) * 19f)
                      + MathF.Sin(y / 19f) * k * (9f + 2f * MathF.Sin(e * 9f - d * 3f + t / 4f));
            float c = d - t / 8f;
            px = q + 60f * MathF.Cos(c) + 200f;
            py = q * MathF.Sin(c) + d * 39f - 195f;
        }

        // One-time centering: bbox of a subsample at t=0, fit to ~80% of the canvas.
        private void ComputeFit()
        {
            int sample = Mathf.Min(_points, 20000);
            float step = ParamRange / sample;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int s = 0; s < sample; s++)
            {
                MapPoint(s * step, 0f, out float px, out float py);
                if (px < minX) minX = px; if (px > maxX) maxX = px;
                if (py < minY) minY = py; if (py > maxY) maxY = py;
            }
            _cx = (minX + maxX) * 0.5f;
            _cy = (minY + maxY) * 0.5f;
            float spanX = Mathf.Max(1e-3f, maxX - minX);
            float spanY = Mathf.Max(1e-3f, maxY - minY);
            _fitScale = 0.8f * Mathf.Min(W / spanX, H / spanY);
        }

        // Merge (parallel paths) + colorize, in ONE Parallel.For over disjoint pixel ranges — each
        // range writes only its own _accum/_pixels slice (no shared write); reads all _threadAccum
        // (shared read). Upload stays on the main thread, after the parallel pass joins.
        // NOTE: this always runs in parallel (even in Sequential mode) and is NOT counted in the
        // "Compute ms" metric — only Accumulate (the point plotting) is timed.
        private void Present(bool fromThreadBuffers)
        {
            int total = W * H;
            int p = _workers;
            int per = Mathf.CeilToInt(total / (float)p);

            Parallel.For(0, p, c =>
            {
                int start = c * per;
                int end = Mathf.Min(total, start + per);
                for (int j = start; j < end; j++)
                {
                    int a;
                    if (fromThreadBuffers)
                    {
                        int sum = 0;
                        for (int t = 0; t < _workers; t++) sum += _threadAccum[t][j];
                        _accum[j] = sum;
                        a = sum;
                    }
                    else
                    {
                        a = _accum[j];
                    }

                    float b = a <= 0 ? 0f : a / (a + 4f); // cheap saturating curve
                    byte v = (byte)(Mathf.Clamp01(0.035f + b) * 255f); // white creature on near-black
                    _pixels[j] = new Color32(v, v, v, 255);
                }
            });

            _tex.SetPixelData(_pixels, 0); // raw upload — cheaper than SetPixels32
            _tex.Apply(false);
        }
    }
}
