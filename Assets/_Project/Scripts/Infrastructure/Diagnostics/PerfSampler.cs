using System;
using Unity.Profiling;
using UnityEngine;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>
    /// Kare süresi örnekleri, %1 low, GC tahsis ve draw call sayaçları.
    /// </summary>
    public sealed class PerfSampler : IDisposable
    {
        public const int DefaultHistory = 240;

        private readonly float[] _frameMs;
        private int _write;
        private int _count;
        private ProfilerRecorder _gcAlloc;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private bool _disposed;

        public PerfSampler(int history = DefaultHistory)
        {
            _frameMs = new float[Mathf.Max(60, history)];
            try
            {
                _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
                _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Perf] ProfilerRecorder başlatılamadı: " + e.Message);
            }
        }

        public int SampleCount => _count;
        public int Capacity => _frameMs.Length;

        public void SampleFrame()
        {
            var ms = Time.unscaledDeltaTime * 1000f;
            _frameMs[_write] = ms;
            _write = (_write + 1) % _frameMs.Length;
            if (_count < _frameMs.Length)
                _count++;
        }

        public float LastFrameMs => _count == 0 ? 0f : _frameMs[(_write - 1 + _frameMs.Length) % _frameMs.Length];

        public float AverageFrameMs()
        {
            if (_count == 0) return 0f;
            double sum = 0;
            for (var i = 0; i < _count; i++)
                sum += _frameMs[i];
            return (float)(sum / _count);
        }

        public float Fps =>
            LastFrameMs > 0.0001f ? 1000f / LastFrameMs : 0f;

        /// <summary>En kötü %1 kare süresi (yüksek ms = düşük fps).</summary>
        public float OnePercentLowMs()
        {
            if (_count == 0) return 0f;
            var tmp = new float[_count];
            Array.Copy(_frameMs, tmp, _count);
            Array.Sort(tmp);
            var index = Mathf.Clamp(_count - 1 - Mathf.Max(0, _count / 100), 0, _count - 1);
            return tmp[index];
        }

        public float OnePercentLowFps()
        {
            var ms = OnePercentLowMs();
            return ms > 0.0001f ? 1000f / ms : 0f;
        }

        public long GcAllocBytesLast => _gcAlloc.Valid ? _gcAlloc.LastValue : 0;
        public long DrawCallsLast => _drawCalls.Valid ? _drawCalls.LastValue : 0;
        public long BatchesLast => _batches.Valid ? _batches.LastValue : 0;

        public void CopyHistory(float[] destination, out int copied)
        {
            copied = 0;
            if (destination == null || destination.Length == 0 || _count == 0)
                return;

            var n = Mathf.Min(destination.Length, _count);
            var start = _count < _frameMs.Length ? 0 : _write;
            for (var i = 0; i < n; i++)
                destination[i] = _frameMs[(start + (_count - n) + i) % _frameMs.Length];
            copied = n;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_gcAlloc.Valid) _gcAlloc.Dispose();
            if (_drawCalls.Valid) _drawCalls.Dispose();
            if (_batches.Valid) _batches.Dispose();
        }
    }
}
