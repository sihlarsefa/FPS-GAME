using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Bir efekt şablonunun örnek havuzu. İhtiyaç oldukça <see cref="Capacity"/>'ye kadar büyür; doluysa en erken
    /// bitecek örneği geri dönüştürür. Oynatma sırasında yönetilen bellek ayırmaz.
    /// </summary>
    internal sealed class ParticleEffectPool
    {
        private readonly ParticleSystem _template;
        private readonly Transform _container;
        private readonly ParticleSystem[] _items;
        private readonly Transform[] _transforms;
        private readonly float[] _busyUntil;
        private readonly float[] _scales;
        private int _count;

        public ParticleEffectPool(EffectKind kind, ParticleSystem template, Transform container, int capacity, float lifetime)
        {
            Kind = kind;
            _template = template;
            _container = container;
            Capacity = Mathf.Max(1, capacity);
            Lifetime = Mathf.Max(0.05f, lifetime);
            _items = new ParticleSystem[Capacity];
            _transforms = new Transform[Capacity];
            _busyUntil = new float[Capacity];
            _scales = new float[Capacity];
        }

        public EffectKind Kind { get; }
        public int Capacity { get; }

        /// <summary>Şablondaki en uzun parçacık görünürlük süresi (s).</summary>
        public float Lifetime { get; }

        public bool IsValid => _template != null && _container != null;

        public int CreatedCount => _count;

        public void Prewarm(int count)
        {
            count = Mathf.Min(count, Capacity);
            while (_count < count)
            {
                if (CreateAt(_count) == null)
                    return;
                _count++;
            }
        }

        /// <summary>
        /// Durmuş, konumlanmış ve ölçeklenmiş bir örnek döndürür (henüz oynatılmamış). Çağıran gerekirse modülleri
        /// ayarlayıp <c>Play(true)</c> çağırır. <paramref name="busySeconds"/> ≤ 0 ise şablon ömrü kullanılır.
        /// </summary>
        public ParticleSystem Prepare(Vector3 position, Quaternion rotation, float scale, float busySeconds)
        {
            var now = Time.time;
            var index = AcquireIndex(now);
            if (index < 0)
                return null;

            var ps = _items[index];
            if (ps == null)
                return null;

            if (_busyUntil[index] > now || ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var t = _transforms[index];
            t.SetPositionAndRotation(position, rotation);
            if (!Mathf.Approximately(_scales[index], scale))
            {
                t.localScale = new Vector3(scale, scale, scale);
                _scales[index] = scale;
            }

            _busyUntil[index] = now + (busySeconds > 0f ? busySeconds : Lifetime);
            return ps;
        }

        public ParticleSystem Spawn(Vector3 position, Quaternion rotation, float scale = 1f)
        {
            var ps = Prepare(position, rotation, scale, 0f);
            if (ps != null)
                ps.Play(true);
            return ps;
        }

        public void StopAll()
        {
            for (var i = 0; i < _count; i++)
            {
                var ps = _items[i];
                if (ps != null)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _busyUntil[i] = 0f;
            }
        }

        private int AcquireIndex(float now)
        {
            // 1) Boşta olan mevcut örnek.
            var oldest = -1;
            var oldestTime = float.MaxValue;
            for (var i = 0; i < _count; i++)
            {
                if (_items[i] == null)
                {
                    // Dışarıdan yok edilmiş (ör. sahne temizliği) — yeniden kur.
                    return CreateAt(i) != null ? i : -1;
                }

                if (_busyUntil[i] <= now)
                    return i;

                if (_busyUntil[i] < oldestTime)
                {
                    oldestTime = _busyUntil[i];
                    oldest = i;
                }
            }

            // 2) Kapasite varsa yeni örnek.
            if (_count < Capacity)
            {
                var index = _count;
                if (CreateAt(index) == null)
                    return oldest;
                _count++;
                return index;
            }

            // 3) En erken bitecek örneği geri dönüştür.
            return oldest;
        }

        private ParticleSystem CreateAt(int index)
        {
            if (!IsValid)
                return null;

            var instance = Object.Instantiate(_template, _container, false);
            instance.gameObject.name = _template.gameObject.name;
            if (!instance.gameObject.activeSelf)
                instance.gameObject.SetActive(true);
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _items[index] = instance;
            _transforms[index] = instance.transform;
            _busyUntil[index] = 0f;
            _scales[index] = 1f;
            instance.transform.localScale = Vector3.one;
            return instance;
        }
    }
}
