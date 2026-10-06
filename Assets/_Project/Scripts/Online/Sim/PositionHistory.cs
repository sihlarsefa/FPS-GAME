using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>
    /// Lag compensation geri sarma verisi: bir savaşçının (konum, dönüş, kapsül) halka tamponu.
    /// 30 Hz × 1 sn. Tick veya zamana göre iki örnek arası interpolasyonlu örnekleme. Saf mantık.
    /// </summary>
    public sealed class PositionHistory
    {
        public readonly struct Sample
        {
            public readonly uint Tick;
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Height;
            public readonly float Radius;

            public Sample(uint tick, float time, Vector3 position, Quaternion rotation, float height, float radius)
            {
                Tick = tick;
                Time = time;
                Position = position;
                Rotation = rotation;
                Height = height;
                Radius = radius;
            }
        }

        private readonly Sample[] _ring;
        private readonly uint _maxRewindTicks;
        private int _start;
        private int _count;

        public PositionHistory(float seconds = 1f, float tickRate = 30f)
        {
            var capacity = Mathf.Clamp(Mathf.CeilToInt(seconds * tickRate) + 2, 8, 256);
            _ring = new Sample[capacity];
            _maxRewindTicks = (uint)Mathf.Max(1, Mathf.RoundToInt(seconds * tickRate));
        }

        public int Capacity => _ring.Length;
        public int Count => _count;
        public uint MaxRewindTicks => _maxRewindTicks;
        public uint NewestTick => _count > 0 ? At(_count - 1).Tick : 0u;
        public uint OldestTick => _count > 0 ? At(0).Tick : 0u;

        private Sample At(int i) => _ring[(_start + i) % _ring.Length];

        /// <summary>Aynı tick tekrar gelirse son örnek güncellenir; geri giden tick reddedilir.</summary>
        public bool Record(uint tick, float time, Vector3 position, Quaternion rotation, float height, float radius)
        {
            var s = new Sample(tick, time, position, rotation, height, radius);
            if (_count > 0)
            {
                var last = At(_count - 1);
                if (tick == last.Tick)
                {
                    _ring[(_start + _count - 1) % _ring.Length] = s;
                    return true;
                }

                if (tick < last.Tick)
                    return false;
            }

            if (_count == _ring.Length)
            {
                _start = (_start + 1) % _ring.Length;
                _count--;
            }

            _ring[(_start + _count) % _ring.Length] = s;
            _count++;
            return true;
        }

        /// <summary>
        /// Atıcının bildirdiği tick'i geri sarma sınırına çeker: gelecek tick → en yeni, 1 sn'den eski → sınır.
        /// Kötü niyetli istemcinin keyfi geçmişe atış yapmasını engeller.
        /// </summary>
        public uint ClampTick(uint requested, uint serverTick)
        {
            if (requested > serverTick)
                return serverTick;
            var oldest = serverTick > _maxRewindTicks ? serverTick - _maxRewindTicks : 0u;
            return requested < oldest ? oldest : requested;
        }

        /// <summary>Tick'e göre interpolasyonlu örnek. Aralık dışı en yakın uca sabitlenir (maxRewind içinde).</summary>
        public bool TrySampleTick(uint tick, out Sample sample)
        {
            sample = default;
            if (_count == 0)
                return false;

            var first = At(0);
            var last = At(_count - 1);
            if (tick <= first.Tick) { sample = first; return true; }
            if (tick >= last.Tick) { sample = last; return true; }

            for (var i = _count - 1; i > 0; i--)
            {
                var a = At(i - 1);
                var b = At(i);
                if (tick >= a.Tick && tick <= b.Tick)
                {
                    var span = b.Tick - a.Tick;
                    var t = span == 0 ? 0f : (float)(tick - a.Tick) / span;
                    sample = Lerp(a, b, t, tick, Mathf.Lerp(a.Time, b.Time, t));
                    return true;
                }
            }

            sample = last;
            return true;
        }

        /// <summary>Dünya zamanına göre interpolasyonlu örnek.</summary>
        public bool TrySampleTime(float time, out Sample sample)
        {
            sample = default;
            if (_count == 0)
                return false;

            var first = At(0);
            var last = At(_count - 1);
            if (time <= first.Time) { sample = first; return true; }
            if (time >= last.Time) { sample = last; return true; }

            for (var i = _count - 1; i > 0; i--)
            {
                var a = At(i - 1);
                var b = At(i);
                if (time >= a.Time && time <= b.Time)
                {
                    var t = (time - a.Time) / Mathf.Max(1e-6f, b.Time - a.Time);
                    sample = Lerp(a, b, t, (uint)Mathf.RoundToInt(Mathf.Lerp(a.Tick, b.Tick, t)), time);
                    return true;
                }
            }

            sample = last;
            return true;
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }

        /// <summary>Kısa yoldan normalize lerp (yönetilen kod; kısa tick aralıklarında slerp'ten farkı ihmal edilir).</summary>
        private static Quaternion Nlerp(Quaternion a, Quaternion b, float t)
        {
            var dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            var sign = dot < 0f ? -1f : 1f;
            var x = a.x + (b.x * sign - a.x) * t;
            var y = a.y + (b.y * sign - a.y) * t;
            var z = a.z + (b.z * sign - a.z) * t;
            var w = a.w + (b.w * sign - a.w) * t;
            var m = (float)System.Math.Sqrt(x * x + y * y + z * z + w * w);
            return m < 1e-6f ? a : new Quaternion(x / m, y / m, z / m, w / m);
        }

        private static Sample Lerp(Sample a, Sample b, float t, uint tick, float time) => new Sample(
            tick, time,
            Vector3.Lerp(a.Position, b.Position, t),
            Nlerp(a.Rotation, b.Rotation, t),
            Mathf.Lerp(a.Height, b.Height, t),
            Mathf.Lerp(a.Radius, b.Radius, t));
    }
}
