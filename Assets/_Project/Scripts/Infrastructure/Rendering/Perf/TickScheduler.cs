using System;
using System.Diagnostics;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Toplu yöneticiye bağlanan nesne; kendi Update'i yerine bunu uygular.</summary>
    public interface IBatchTickable
    {
        /// <summary>dt: bu nesnenin önceki Tick'inden bu yana geçen süre (sn).</summary>
        void BatchTick(float dt);
    }

    /// <summary>
    /// Update yerine toplu yönetici: yüzlerce MonoBehaviour.Update (her biri yerel->yönetilen çağrı maliyeti)
    /// yerine tek döngü. Her kayıt kendi aralığıyla çalışır; başlangıç fazları dağıtılır (aynı karede
    /// hepsi çalışıp tepe yaratmasın) ve kare başına ms bütçesi aşılırsa kalanlar sonraki kareye devredilir
    /// (en çok gecikmiş önce). Saf C#: zaman dışarıdan verilir.
    /// </summary>
    public sealed class TickScheduler
    {
        private struct Entry
        {
            public IBatchTickable Target;
            public float Interval;
            public double Next;
            public double Last;
            public bool Dead;
        }

        private Entry[] _entries = new Entry[32];
        private int _count;
        private int _cursor;
        private int _registered; // faz dağıtımı için artan sayaç
        private bool _running;
        private int _deadPending;
        private readonly Action<Exception> _onError;

        public TickScheduler(Action<Exception> onError = null) { _onError = onError; }

        public int Count => _count - _deadPending;
        /// <summary>Son Run'da bütçe/limit nedeniyle ertelenen vadesi gelmiş kayıt sayısı.</summary>
        public int LastDeferred { get; private set; }
        public int LastRan { get; private set; }

        /// <summary>
        /// Kaydeder. interval 0 = her Run'da. Faz dağıtımı: n'inci kayıt, aralığın kesirli bir payı kadar kaydırılır
        /// (altın oran kesri; sıralı kayıtlar bile eşit yayılır).
        /// </summary>
        public bool Register(IBatchTickable target, float interval, double now)
        {
            if (target == null) return false;
            for (var i = 0; i < _count; i++)
                if (!_entries[i].Dead && ReferenceEquals(_entries[i].Target, target)) return false;
            if (interval < 0f) interval = 0f;
            if (_count == _entries.Length) Array.Resize(ref _entries, _count * 2);
            var phase = Frac(_registered++ * 0.6180339887);
            _entries[_count++] = new Entry
            {
                Target = target, Interval = interval,
                Last = now, Next = now + interval * phase, Dead = false
            };
            return true;
        }

        public bool Unregister(IBatchTickable target)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i].Dead || !ReferenceEquals(_entries[i].Target, target)) continue;
                _entries[i].Dead = true;
                _entries[i].Target = null;
                _deadPending++;
                if (!_running) Compact();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Vadesi gelenleri çalıştırır. budgetMs &lt;= 0: sınırsız. Her çağrıda başlangıç imleci ilerler
        /// (adil sıra); bütçe dolarsa kalanlar bir sonraki çağrıya kalır ve gecikme arttıkça öncelik kazanır.
        /// </summary>
        public int Run(double now, double budgetMs = 0.0)
        {
            LastDeferred = 0; LastRan = 0;
            if (_count == 0) return 0;
            _running = true;
            var startTs = Stopwatch.GetTimestamp();
            var budgetTicks = budgetMs > 0.0 ? (long)(budgetMs * Stopwatch.Frequency / 1000.0) : long.MaxValue;
            var n = _count; // Run sırasında eklenenler sonraki turda
            var ran = 0;
            var firstSkipped = -1;
            var over = false;
            var start = _cursor % n;

            for (var k = 0; k < n; k++)
            {
                var i = (start + k) % n;
                ref var e = ref _entries[i];
                if (e.Dead || e.Next > now) continue;
                if (over || (ran > 0 && Stopwatch.GetTimestamp() - startTs > budgetTicks))
                {
                    over = true;
                    LastDeferred++;
                    if (firstSkipped < 0) firstSkipped = i;
                    continue;
                }
                var dt = (float)(now - e.Last);
                var target = e.Target;
                e.Last = now;
                // Geride kaldıysa birikmiş kareleri art arda çalıştırma: bir sonraki vade now+interval.
                e.Next = now + e.Interval;
                ran++;
                try { target.BatchTick(dt); }
                catch (Exception ex) { _onError?.Invoke(ex); }
            }

            // Bütçe yetmediyse ertelenenlerden başla (açlık önleme); aksi halde imleci ilerlet.
            _cursor = firstSkipped >= 0 ? firstSkipped : (start + 1) % Math.Max(1, n);
            _running = false;
            if (_deadPending > 0) Compact();
            LastRan = ran;
            return ran;
        }

        public void Clear()
        {
            Array.Clear(_entries, 0, _count);
            _count = 0; _cursor = 0; _deadPending = 0; _registered = 0;
        }

        private void Compact()
        {
            var w = 0;
            for (var r = 0; r < _count; r++)
            {
                if (_entries[r].Dead) continue;
                if (w != r) _entries[w] = _entries[r];
                w++;
            }
            Array.Clear(_entries, w, _count - w);
            _count = w; _deadPending = 0; _cursor = 0;
        }

        private static double Frac(double x) => x - Math.Floor(x);
    }
}
