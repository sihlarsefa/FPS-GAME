using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Pooling
{
    /// <summary>Havuz sayaçları; ölçüm/test için salt okunur görünüm.</summary>
    public readonly struct PoolStats
    {
        public readonly int Total, Active, Inactive, PeakActive, Misses, Overflows, DoubleReleases;
        public PoolStats(int total, int active, int inactive, int peak, int misses, int overflows, int doubles)
        { Total = total; Active = active; Inactive = inactive; PeakActive = peak; Misses = misses; Overflows = overflows; DoubleReleases = doubles; }
    }

    /// <summary>
    /// Genel amaçlı, Unity'den bağımsız nesne havuzu. Sıcak yolda Get/Release ayırma yapmaz
    /// (havuz boşken fabrika çağrısı hariç; "Misses" sayacı bunu gösterir).
    /// Çift iade (double release) tespit edilir ve yok sayılır.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _free;
        private readonly HashSet<T> _freeSet; // yalnız çift iade denetimi açıkken
        private readonly Func<T> _create;
        private readonly Action<T> _onGet, _onRelease, _onDestroy;
        private readonly int _maxSize;
        private int _total, _active, _peak, _misses, _overflows, _doubles;

        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null,
            Action<T> onDestroy = null, int maxSize = 256, bool detectDoubleRelease = true)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet; _onRelease = onRelease; _onDestroy = onDestroy;
            _maxSize = Math.Max(1, maxSize);
            _free = new Stack<T>(Math.Min(_maxSize, 64));
            if (detectDoubleRelease) _freeSet = new HashSet<T>();
        }

        public int MaxSize => _maxSize;
        public int CountInactive => _free.Count;
        public int CountActive => _active;
        public int CountAll => _total;

        public PoolStats Stats => new PoolStats(_total, _active, _free.Count, _peak, _misses, _overflows, _doubles);

        /// <summary>Havuzdan nesne verir; boşsa fabrikayla üretir.</summary>
        public T Get()
        {
            T item;
            if (_free.Count > 0)
            {
                item = _free.Pop();
                _freeSet?.Remove(item);
            }
            else
            {
                item = _create();
                _total++;
                _misses++;
            }
            _active++;
            if (_active > _peak) _peak = _active;
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>Nesneyi iade eder. Çift iade veya null ise false döner. Havuz doluysa nesne yok edilir.</summary>
        public bool Release(T item)
        {
            if (item == null) return false;
            if (_freeSet != null && _freeSet.Contains(item)) { _doubles++; return false; }
            if (_active > 0) _active--;
            if (_free.Count >= _maxSize)
            {
                _overflows++;
                _total--;
                _onDestroy?.Invoke(item);
                return true;
            }
            _onRelease?.Invoke(item);
            _free.Push(item);
            _freeSet?.Add(item);
            return true;
        }

        /// <summary>Alınmış ama artık geçersiz (ör. dışarıdan yok edilmiş) bir nesneyi sayaçlardan düşer.</summary>
        public void Forget()
        {
            if (_active > 0) _active--;
            if (_total > 0) _total--;
        }

        /// <summary>Yükleme anında önceden üretir; oyun içi ilk-kullanım takılmasını önler.</summary>
        public int Prewarm(int count)
        {
            var made = 0;
            var target = Math.Min(Math.Max(0, count), _maxSize);
            while (_free.Count < target)
            {
                var item = _create();
                _total++;
                _onRelease?.Invoke(item);
                _free.Push(item);
                _freeSet?.Add(item);
                made++;
            }
            return made;
        }

        /// <summary>Boştaki nesneleri keep sayısına indirir (bellek baskısında).</summary>
        public int Trim(int keep)
        {
            var removed = 0;
            keep = Math.Max(0, keep);
            while (_free.Count > keep)
            {
                var item = _free.Pop();
                _freeSet?.Remove(item);
                _total--;
                _onDestroy?.Invoke(item);
                removed++;
            }
            return removed;
        }

        /// <summary>Tüm boştaki nesneleri yok eder ve sayaçları sıfırlar (aktifler kullanıcıda kalır).</summary>
        public void Clear()
        {
            Trim(0);
            _peak = _active; _misses = 0; _overflows = 0; _doubles = 0;
        }
    }
}
