using System;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Son 1 saniyelik hitbox geçmişi (lag compensation). 30 Hz × 1 sn ≈ 30 örnek.
    /// </summary>
    public sealed class LagCompensationBuffer
    {
        private readonly HitboxSample[] _samples;
        private int _count;
        private int _head;

        public LagCompensationBuffer(float seconds = 1f, float tickRate = 30f)
        {
            var capacity = Mathf.Clamp(Mathf.CeilToInt(seconds * tickRate) + 2, 8, 128);
            _samples = new HitboxSample[capacity];
        }

        public int Capacity => _samples.Length;
        public int Count => _count;

        public void Record(uint tick, Vector3 position, Quaternion rotation, float height, float radius)
        {
            _samples[_head] = new HitboxSample(tick, Time.time, position, rotation, height, radius);
            _head = (_head + 1) % _samples.Length;
            if (_count < _samples.Length)
                _count++;
        }

        public bool TrySample(uint tick, out CapsuleHitbox hitbox)
        {
            hitbox = default;
            if (_count == 0)
                return false;

            HitboxSample? best = null;
            var bestDelta = uint.MaxValue;
            for (var i = 0; i < _count; i++)
            {
                var idx = (_head - 1 - i + _samples.Length * 2) % _samples.Length;
                var sample = _samples[idx];
                var delta = tick >= sample.Tick ? tick - sample.Tick : sample.Tick - tick;
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = sample;
                }
            }

            if (best == null)
                return false;

            var s = best.Value;
            hitbox = new CapsuleHitbox(s.Position, s.Rotation, s.Height, s.Radius);
            return true;
        }

        public bool TrySampleAtTime(float worldTime, out CapsuleHitbox hitbox)
        {
            hitbox = default;
            if (_count == 0)
                return false;

            HitboxSample? best = null;
            var bestAbs = float.MaxValue;
            for (var i = 0; i < _count; i++)
            {
                var idx = (_head - 1 - i + _samples.Length * 2) % _samples.Length;
                var sample = _samples[idx];
                var abs = Mathf.Abs(sample.Time - worldTime);
                if (abs < bestAbs)
                {
                    bestAbs = abs;
                    best = sample;
                }
            }

            if (best == null)
                return false;

            var s = best.Value;
            hitbox = new CapsuleHitbox(s.Position, s.Rotation, s.Height, s.Radius);
            return true;
        }

        private readonly struct HitboxSample
        {
            public readonly uint Tick;
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Height;
            public readonly float Radius;

            public HitboxSample(uint tick, float time, Vector3 position, Quaternion rotation, float height, float radius)
            {
                Tick = tick;
                Time = time;
                Position = position;
                Rotation = rotation;
                Height = height;
                Radius = radius;
            }
        }
    }
}
