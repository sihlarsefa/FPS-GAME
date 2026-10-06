using System;

namespace Project.Infrastructure.Pooling
{
    /// <summary>
    /// "X saniye sonra havuza iade et" işlerini tek min-yığınla yönetir. VFX/iz/kovan gibi her biri kendi
    /// Update'ini ya da Invoke/Coroutine'ini kullanmak yerine tek Tick ile hepsi serbest bırakılır.
    /// Dizi kapasitesi dolunca ikiye katlanır (nadir); normal çalışmada ayırma yok.
    /// </summary>
    public sealed class TimedReleaseScheduler<T> where T : class
    {
        private double[] _due;
        private T[] _items;
        private int _count;

        public TimedReleaseScheduler(int initialCapacity = 64)
        {
            var c = Math.Max(4, initialCapacity);
            _due = new double[c];
            _items = new T[c];
        }

        public int Count => _count;
        public double NextDue => _count == 0 ? double.PositiveInfinity : _due[0];

        public void Schedule(T item, double dueTime)
        {
            if (item == null) return;
            if (_count == _due.Length)
            {
                Array.Resize(ref _due, _count * 2);
                Array.Resize(ref _items, _count * 2);
            }
            var i = _count++;
            while (i > 0)
            {
                var parent = (i - 1) >> 1;
                if (_due[parent] <= dueTime) break;
                _due[i] = _due[parent]; _items[i] = _items[parent];
                i = parent;
            }
            _due[i] = dueTime; _items[i] = item;
        }

        /// <summary>Vadesi gelenleri sırayla release'e verir; en fazla maxPerCall (0 = sınırsız). Verilen sayıyı döner.</summary>
        public int Tick(double now, Action<T> release, int maxPerCall = 0)
        {
            var done = 0;
            while (_count > 0 && _due[0] <= now)
            {
                if (maxPerCall > 0 && done >= maxPerCall) break;
                var item = _items[0];
                PopRoot();
                done++;
                release?.Invoke(item);
            }
            return done;
        }

        /// <summary>Hepsini hemen serbest bırakır (sahne değişimi).</summary>
        public int Flush(Action<T> release)
        {
            var n = _count;
            while (_count > 0)
            {
                var item = _items[0];
                PopRoot();
                release?.Invoke(item);
            }
            return n;
        }

        private void PopRoot()
        {
            _count--;
            if (_count == 0) { _items[0] = null; return; }
            var lastDue = _due[_count];
            var lastItem = _items[_count];
            _items[_count] = null;
            var i = 0;
            while (true)
            {
                var child = 2 * i + 1;
                if (child >= _count) break;
                if (child + 1 < _count && _due[child + 1] < _due[child]) child++;
                if (_due[child] >= lastDue) break;
                _due[i] = _due[child]; _items[i] = _items[child];
                i = child;
            }
            _due[i] = lastDue; _items[i] = lastItem;
        }
    }
}
