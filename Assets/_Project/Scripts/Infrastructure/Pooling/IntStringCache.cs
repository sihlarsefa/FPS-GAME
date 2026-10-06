using System.Globalization;

namespace Project.Infrastructure.Pooling
{
    /// <summary>
    /// Tamsayı -> metin önbelleği (mermi sayısı, HP, FPS...). Her karede value.ToString() çağrısı
    /// 20-30 baytlık çöp üretir; burada her değer yalnız bir kez üretilir.
    /// </summary>
    public sealed class IntStringCache
    {
        private readonly string[] _cache;
        private readonly int _min;

        public IntStringCache(int min = 0, int max = 999)
        {
            _min = min;
            _cache = new string[System.Math.Max(1, max - min + 1)];
        }

        public int CachedCount
        {
            get { var n = 0; for (var i = 0; i < _cache.Length; i++) if (_cache[i] != null) n++; return n; }
        }

        public string Get(int value)
        {
            var i = value - _min;
            if ((uint)i >= (uint)_cache.Length) return value.ToString(CultureInfo.InvariantCulture); // aralık dışı: nadir
            return _cache[i] ?? (_cache[i] = value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
