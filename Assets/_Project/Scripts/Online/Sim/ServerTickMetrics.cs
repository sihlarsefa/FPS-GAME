using System;

namespace Project.Online.Sim
{
    /// <summary>
    /// Sunucu tick bütçesi ölçümü (saf): son N karenin işlem süresi, bütçe (1/tickHz) aşım oranı, ortalama / p95 / maks.
    /// Allocation-free halka tampon.
    /// </summary>
    public sealed class ServerTickMetrics
    {
        private readonly float[] _ring;
        private readonly float[] _scratch;
        private int _head;
        private int _count;

        public float BudgetSeconds { get; }
        public long TotalSamples { get; private set; }
        public long TotalOverruns { get; private set; }
        public float WorstSeconds { get; private set; }

        public ServerTickMetrics(float tickRateHz = 30f, int window = 120)
        {
            BudgetSeconds = 1f / Math.Max(1f, tickRateHz);
            var n = Math.Max(8, window);
            _ring = new float[n];
            _scratch = new float[n];
        }

        public int Count => _count;

        public void Record(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                return;

            _ring[_head] = seconds;
            _head = (_head + 1) % _ring.Length;
            if (_count < _ring.Length)
                _count++;

            TotalSamples++;
            if (seconds > BudgetSeconds)
                TotalOverruns++;
            if (seconds > WorstSeconds)
                WorstSeconds = seconds;
        }

        public float Average
        {
            get
            {
                if (_count == 0) return 0f;
                var sum = 0f;
                for (var i = 0; i < _count; i++) sum += _ring[i];
                return sum / _count;
            }
        }

        public float WindowMax
        {
            get
            {
                var m = 0f;
                for (var i = 0; i < _count; i++) if (_ring[i] > m) m = _ring[i];
                return m;
            }
        }

        /// <summary>Penceredeki örneklerin en yüksek BudgetSeconds'u aşan oranı (0..1).</summary>
        public float OverrunRatio
        {
            get
            {
                if (_count == 0) return 0f;
                var over = 0;
                for (var i = 0; i < _count; i++) if (_ring[i] > BudgetSeconds) over++;
                return (float)over / _count;
            }
        }

        public float Percentile(float p)
        {
            if (_count == 0) return 0f;
            Array.Copy(_ring, _scratch, _count);
            Array.Sort(_scratch, 0, _count);
            var idx = (int)Math.Ceiling(Math.Min(1f, Math.Max(0f, p)) * _count) - 1;
            return _scratch[Math.Max(0, Math.Min(_count - 1, idx))];
        }

        /// <summary>Ortalama bütçenin yüzdesi (100 = tam bütçe).</summary>
        public float BudgetUsagePercent => BudgetSeconds > 0f ? Average / BudgetSeconds * 100f : 0f;

        /// <summary>Sağlıklı: aşım oranı %5 altı ve ortalama bütçenin %80'i altı.</summary>
        public bool IsHealthy => OverrunRatio < 0.05f && BudgetUsagePercent < 80f;

        public string Summary() =>
            $"tick avg={Average * 1000f:F1}ms p95={Percentile(0.95f) * 1000f:F1}ms max={WindowMax * 1000f:F1}ms " +
            $"budget={BudgetSeconds * 1000f:F1}ms kullanım=%{BudgetUsagePercent:F0} aşım=%{OverrunRatio * 100f:F0}";

        public void Reset()
        {
            _head = 0;
            _count = 0;
            TotalSamples = 0;
            TotalOverruns = 0;
            WorstSeconds = 0f;
        }
    }
}
