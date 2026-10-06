using System;

namespace Project.Infrastructure.Vfx.Impacts
{
    /// <summary>
    /// Seri ateş/pompalı saçması için isabet kısıtlayıcı (saf, zaman dışarıdan verilir). Aynı yüzeyde,
    /// kısa pencerede, birbirine çok yakın isabetleri birleştirir (parçacık atla, çıkartma bırak) ve
    /// pencere başına parçacık efekti sayısını sınırlar.
    /// </summary>
    public sealed class ImpactBurstLimiter
    {
        private readonly float _window;
        private readonly int _maxPerWindow;
        private readonly float _mergeRadiusSqr;
        private readonly float[] _times;
        private readonly float[] _x, _y, _z;
        private readonly int[] _surf;
        private int _cursor;
        private int _windowCount;
        private float _windowStart = float.NegativeInfinity;

        public ImpactBurstLimiter(float window, int maxPerWindow, float mergeRadius, int memory)
        {
            _window = Math.Max(0.01f, window);
            _maxPerWindow = Math.Max(1, maxPerWindow);
            _mergeRadiusSqr = mergeRadius * mergeRadius;
            var m = Math.Max(1, memory);
            _times = new float[m];
            _x = new float[m]; _y = new float[m]; _z = new float[m];
            _surf = new int[m];
            for (var i = 0; i < m; i++) _times[i] = float.NegativeInfinity;
        }

        /// <summary>true: parçacık efekti oynat. Her durumda isabet kaydedilir.</summary>
        public bool TryAcceptParticles(float now, float x, float y, float z, SurfaceKind surface)
        {
            if (now - _windowStart > _window)
            {
                _windowStart = now;
                _windowCount = 0;
            }

            var merged = false;
            for (var i = 0; i < _times.Length; i++)
            {
                if (now - _times[i] > _window || _surf[i] != (int)surface) continue;
                var dx = _x[i] - x; var dy = _y[i] - y; var dz = _z[i] - z;
                if (dx * dx + dy * dy + dz * dz <= _mergeRadiusSqr) { merged = true; break; }
            }

            _times[_cursor] = now; _x[_cursor] = x; _y[_cursor] = y; _z[_cursor] = z; _surf[_cursor] = (int)surface;
            _cursor = (_cursor + 1) % _times.Length;

            if (merged || _windowCount >= _maxPerWindow) return false;
            _windowCount++;
            return true;
        }
    }
}
