using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Audio.Ambience
{
    public enum DistantKind
    {
        Gunshot = 0,
        Blast
    }

    /// <summary>Planlanmış uzak çatışma sesi (varış zamanı ses hızına göre gecikmiştir).</summary>
    public struct DistantEvent
    {
        public DistantKind Kind;
        public float DueTime;
        public float Distance;
        public float AngleDeg;
        public float Volume;
        public float LowpassHz;
        public float Pitch;
        public bool Synthetic;
    }

    /// <summary>
    /// Vadi boyunca duyulan uzak çatışma: gerçek maç olaylarından (WeaponFired/Explosion) ses hızı gecikmesiyle
    /// zamanlanır; gerçek olay yoksa belirli aralıklarla sentetik atış dizileri ve patlamalar üretir. Saf mantık.
    /// </summary>
    public sealed class DistantBattleScheduler
    {
        public const float SpeedOfSound = 343f;
        public const float MinDistance = 140f;
        public const float MaxDistance = 1500f;
        public const int MaxQueue = 24;
        /// <summary>Otomatik ateşi seyreltmek için iki gerçek atış arasında asgari süre.</summary>
        public const float MinShotSpacing = 0.11f;
        public const float RealIdleBeforeSyntheticSeconds = 18f;
        public const float SyntheticBurstGapMin = 7f;
        public const float SyntheticBurstGapMax = 22f;

        private readonly List<DistantEvent> _queue = new List<DistantEvent>(MaxQueue);
        private AmbienceRng _rng;
        private float _lastReal = -1000f;
        private float _lastShotAccepted = -1000f;
        private float _nextSynthetic;
        private int _syntheticLeft;
        private float _syntheticNextShot;
        private float _syntheticAngle;
        private float _syntheticDistance;

        public bool SyntheticEnabled = true;
        public int Count => _queue.Count;
        public float LastRealTime => _lastReal;

        public DistantBattleScheduler(uint seed = 777u)
        {
            _rng = new AmbienceRng(seed);
            _nextSynthetic = 6f;
        }

        public static float VolumeFor(DistantKind kind, float distance)
        {
            var d = Math.Max(MinDistance, distance);
            var v = (float)Math.Pow(MinDistance / d, 0.85) * (kind == DistantKind.Blast ? 0.75f : 0.5f);
            return Math.Min(0.8f, Math.Max(0.03f, v));
        }

        public static float LowpassFor(float distance)
        {
            var t = Math.Min(1f, Math.Max(0f, (distance - MinDistance) / (MaxDistance - MinDistance)));
            return 5200f - 4200f * t;
        }

        public static float DelayFor(float distance) => Math.Max(0f, distance) / SpeedOfSound;

        /// <summary>Dinleyicinin etki alanı dışındaki gerçek bir olayı (atış/patlama) planlar. Kabul edildiyse true.</summary>
        public bool OnRealEvent(DistantKind kind, float distance, float angleDeg, float now)
        {
            if (distance < MinDistance || distance > MaxDistance || float.IsNaN(distance))
                return false;
            _lastReal = now;
            if (kind == DistantKind.Gunshot)
            {
                if (now - _lastShotAccepted < MinShotSpacing)
                    return false;
                _lastShotAccepted = now;
            }

            return Enqueue(new DistantEvent
            {
                Kind = kind,
                DueTime = now + DelayFor(distance),
                Distance = distance,
                AngleDeg = angleDeg,
                Volume = VolumeFor(kind, distance),
                LowpassHz = LowpassFor(distance),
                Pitch = kind == DistantKind.Blast ? _rng.Range(0.7f, 0.9f) : _rng.Range(0.95f, 1.05f),
                Synthetic = false
            });
        }

        private bool Enqueue(DistantEvent e)
        {
            if (_queue.Count >= MaxQueue)
            {
                // En sessiz olayı at.
                var weakest = 0;
                for (var i = 1; i < _queue.Count; i++)
                    if (_queue[i].Volume < _queue[weakest].Volume)
                        weakest = i;
                if (_queue[weakest].Volume >= e.Volume)
                    return false;
                _queue.RemoveAt(weakest);
            }

            _queue.Add(e);
            return true;
        }

        /// <summary>Zamanı gelen bir olayı verir (her çağrıda en fazla bir). Sentetik yedeği de burada üretir.</summary>
        public bool TryPop(float now, out DistantEvent result)
        {
            TickSynthetic(now);
            for (var i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].DueTime > now)
                    continue;
                result = _queue[i];
                _queue.RemoveAt(i);
                return true;
            }

            result = default;
            return false;
        }

        private void TickSynthetic(float now)
        {
            if (!SyntheticEnabled || now - _lastReal < RealIdleBeforeSyntheticSeconds)
            {
                _syntheticLeft = 0;
                return;
            }

            if (_syntheticLeft > 0)
            {
                if (now < _syntheticNextShot)
                    return;
                _syntheticLeft--;
                _syntheticNextShot = now + _rng.Range(0.12f, 0.32f);
                Enqueue(new DistantEvent
                {
                    Kind = DistantKind.Gunshot,
                    DueTime = now,
                    Distance = _syntheticDistance,
                    AngleDeg = _syntheticAngle + _rng.Range(-4f, 4f),
                    Volume = VolumeFor(DistantKind.Gunshot, _syntheticDistance) * _rng.Range(0.6f, 1f),
                    LowpassHz = LowpassFor(_syntheticDistance),
                    Pitch = _rng.Range(0.95f, 1.05f),
                    Synthetic = true
                });
                return;
            }

            if (now < _nextSynthetic)
                return;

            _nextSynthetic = now + _rng.Range(SyntheticBurstGapMin, SyntheticBurstGapMax);
            _syntheticAngle = _rng.Range(0f, 360f);
            _syntheticDistance = _rng.Range(300f, 1100f);
            if (_rng.Chance(0.3f))
            {
                Enqueue(new DistantEvent
                {
                    Kind = DistantKind.Blast,
                    DueTime = now,
                    Distance = _syntheticDistance,
                    AngleDeg = _syntheticAngle,
                    Volume = VolumeFor(DistantKind.Blast, _syntheticDistance),
                    LowpassHz = LowpassFor(_syntheticDistance),
                    Pitch = _rng.Range(0.7f, 0.9f),
                    Synthetic = true
                });
            }
            else
            {
                _syntheticLeft = _rng.RangeInt(3, 8);
                _syntheticNextShot = now;
            }
        }

        public void Clear()
        {
            _queue.Clear();
            _syntheticLeft = 0;
        }
    }
}
