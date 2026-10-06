using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>Snapshot varış aralığından ağ jitter'ı kestirir ve interpolasyon gecikmesini uyarlar.</summary>
    public sealed class JitterEstimator
    {
        private readonly float _nominalInterval;
        private readonly float _minDelay;
        private readonly float _maxDelay;
        private float _meanInterval;
        private float _deviation;
        private float _lastArrival = float.NaN;
        private float _delay;

        public JitterEstimator(float tickRate = 30f, float minDelay = 0f, float maxDelay = 0.4f)
        {
            _nominalInterval = 1f / Mathf.Max(1f, tickRate);
            _minDelay = minDelay > 0f ? minDelay : _nominalInterval * 2f;
            _maxDelay = Mathf.Max(_minDelay, maxDelay);
            _meanInterval = _nominalInterval;
            _delay = _minDelay;
        }

        public float Jitter => _deviation;
        public float MeanInterval => _meanInterval;
        /// <summary>Şu anki (yumuşatılmış) interpolasyon gecikmesi, sn.</summary>
        public float Delay => _delay;
        public float TargetDelay => Mathf.Clamp(_meanInterval + 3f * _deviation + _nominalInterval, _minDelay, _maxDelay);

        public void Observe(float arrivalTime)
        {
            if (!float.IsNaN(_lastArrival))
            {
                var dt = arrivalTime - _lastArrival;
                if (dt > 0f && dt < 2f)
                {
                    _meanInterval += 0.1f * (dt - _meanInterval);
                    _deviation += 0.1f * (Mathf.Abs(dt - _meanInterval) - _deviation);
                }
            }

            _lastArrival = arrivalTime;
        }

        /// <summary>Gecikmeyi hedefe sınırlı hızla (50 ms/sn) kaydırır: render zamanı sıçramaz. Yükselirken hızlı.</summary>
        public float Advance(float dt)
        {
            var target = TargetDelay;
            var rate = target > _delay ? 0.25f : 0.05f;
            _delay = Mathf.MoveTowards(_delay, target, rate * Mathf.Max(0f, dt));
            return _delay;
        }
    }

    /// <summary>Uzak varlık için zaman damgalı snapshot tamponu + interpolasyon / kısa ekstrapolasyon.</summary>
    public sealed class SnapshotInterpolator
    {
        public struct Pose
        {
            public float Time;
            public Vector3 Position;
            public float Yaw;
        }

        public const float MaxExtrapolation = 0.2f;

        private readonly Pose[] _ring;
        private int _start;
        private int _count;

        public SnapshotInterpolator(int capacity = 32)
        {
            _ring = new Pose[Mathf.Max(4, capacity)];
        }

        public int Count => _count;
        public float NewestTime => _count > 0 ? At(_count - 1).Time : float.NaN;

        private Pose At(int i) => _ring[(_start + i) % _ring.Length];

        /// <summary>Sunucu zaman damgasıyla snapshot ekler; eski/aynı damga yok sayılır.</summary>
        public bool Add(float serverTime, Vector3 position, float yaw)
        {
            if (_count > 0 && serverTime <= At(_count - 1).Time)
                return false;

            if (_count == _ring.Length)
            {
                _start = (_start + 1) % _ring.Length;
                _count--;
            }

            _ring[(_start + _count) % _ring.Length] = new Pose { Time = serverTime, Position = position, Yaw = yaw };
            _count++;
            return true;
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }

        /// <summary>renderTime (sunucu zaman ekseni) için poz. Tampon boşsa false.</summary>
        public bool TrySample(float renderTime, out Pose pose)
        {
            pose = default;
            if (_count == 0)
                return false;

            var first = At(0);
            if (renderTime <= first.Time || _count == 1)
            {
                pose = first;
                pose.Time = renderTime;
                return true;
            }

            var last = At(_count - 1);
            if (renderTime >= last.Time)
            {
                // Kısa ekstrapolasyon (son hız), sonra bekle.
                var prev = At(_count - 2);
                var span = last.Time - prev.Time;
                var ahead = Mathf.Min(renderTime - last.Time, MaxExtrapolation);
                var vel = span > 1e-4f ? (last.Position - prev.Position) / span : Vector3.zero;
                pose = new Pose { Time = renderTime, Position = last.Position + vel * ahead, Yaw = last.Yaw };
                return true;
            }

            for (var i = _count - 1; i > 0; i--)
            {
                var a = At(i - 1);
                var b = At(i);
                if (renderTime >= a.Time && renderTime <= b.Time)
                {
                    var t = (renderTime - a.Time) / Mathf.Max(1e-5f, b.Time - a.Time);
                    pose = new Pose
                    {
                        Time = renderTime,
                        Position = Vector3.Lerp(a.Position, b.Position, t),
                        Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t)
                    };
                    return true;
                }
            }

            pose = last;
            return true;
        }
    }
}
