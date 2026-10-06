using System;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Son N karenin özet ölçüleri.</summary>
    public struct FrameStats
    {
        public int Samples;
        public float AvgMs, MinMs, MaxMs, MedianMs, P95Ms, P99Ms;
        /// <summary>Kare süresi dağılımının en kötü %1'inin ortalamasından FPS ("1% low").</summary>
        public float OnePercentLowFps;
        public float AvgFps;
        /// <summary>Ardışık karelerin mutlak fark ortalaması (ms) = kare zamanlama düzgünlüğü (jitter).</summary>
        public float JitterMs;
        /// <summary>Eşiği aşan kare sayısı (takılma).</summary>
        public int HitchCount;
    }

    /// <summary>
    /// Kare süresi izleyicisi: sabit tampon, ölçüm sırasında ayırma yok. Oyuncunun hissettiği şey
    /// ortalama FPS değil kare süresi dağılımının kuyruğudur (p95/p99, 1% low) ve karelerin düzgünlüğüdür.
    /// </summary>
    public sealed class FrameTimeTracker
    {
        private readonly float[] _ring;
        private readonly float[] _scratch;
        private int _head, _count;

        public FrameTimeTracker(int capacity = 240)
        {
            if (capacity < 8) capacity = 8;
            _ring = new float[capacity];
            _scratch = new float[capacity];
        }

        public int Count => _count;
        public int Capacity => _ring.Length;

        /// <summary>Bir kare süresi ekler (saniye). Geçersiz/negatif değerler yok sayılır; 1 sn'ye kırpılır.</summary>
        public void Push(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || deltaSeconds <= 0f) return;
            if (deltaSeconds > 1f) deltaSeconds = 1f;
            _ring[_head] = deltaSeconds * 1000f;
            _head = (_head + 1) % _ring.Length;
            if (_count < _ring.Length) _count++;
        }

        public void Clear() { _head = 0; _count = 0; }

        /// <summary>İstatistik üretir. hitchMs: bu süreyi aşan kare "takılma" sayılır (0 = 2x medyan, en az 8 ms).</summary>
        public FrameStats Compute(float hitchMs = 0f)
        {
            var s = new FrameStats { Samples = _count };
            if (_count == 0) return s;

            var start = _count == _ring.Length ? _head : 0;
            double sum = 0, jitter = 0;
            float min = float.MaxValue, max = 0f, prev = 0f;
            for (var i = 0; i < _count; i++)
            {
                var v = _ring[(start + i) % _ring.Length];
                _scratch[i] = v;
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
                if (i > 0) jitter += Math.Abs(v - prev);
                prev = v;
            }
            Array.Sort(_scratch, 0, _count);

            s.AvgMs = (float)(sum / _count);
            s.MinMs = min; s.MaxMs = max;
            s.MedianMs = Percentile(0.5f);
            s.P95Ms = Percentile(0.95f);
            s.P99Ms = Percentile(0.99f);
            s.JitterMs = _count > 1 ? (float)(jitter / (_count - 1)) : 0f;
            s.AvgFps = s.AvgMs > 0f ? 1000f / s.AvgMs : 0f;

            // En kötü %1 (en az 1 kare) ortalaması.
            var worstN = Math.Max(1, _count / 100);
            double worstSum = 0;
            for (var i = _count - worstN; i < _count; i++) worstSum += _scratch[i];
            var worstAvg = (float)(worstSum / worstN);
            s.OnePercentLowFps = worstAvg > 0f ? 1000f / worstAvg : 0f;

            var threshold = hitchMs > 0f ? hitchMs : Math.Max(8f, s.MedianMs * 2f);
            var hitches = 0;
            for (var i = _count - 1; i >= 0 && _scratch[i] > threshold; i--) hitches++;
            s.HitchCount = hitches;
            return s;
        }

        // _scratch sıralı olmalı.
        private float Percentile(float p)
        {
            if (_count == 1) return _scratch[0];
            var pos = p * (_count - 1);
            var lo = (int)pos;
            var hi = Math.Min(lo + 1, _count - 1);
            var f = pos - lo;
            return _scratch[lo] + (_scratch[hi] - _scratch[lo]) * f;
        }
    }
}
