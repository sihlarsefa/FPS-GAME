using System;

namespace Project.Infrastructure.Audio.Ambience
{
    /// <summary>Uzak top sesi zamanlayıcısı: 40-90 sn rastgele aralık (saf mantık).</summary>
    public sealed class DistantCannonTimer
    {
        public const float MinInterval = 40f;
        public const float MaxInterval = 90f;

        private AmbienceRng _rng;
        private float _next = -1f;

        public DistantCannonTimer(uint seed = 777u) => _rng = new AmbienceRng(seed);

        /// <summary>Zamanı geldiyse top olayı üretir ve sonraki aralığı seçer.</summary>
        public bool TryTick(float now, out DistantEvent e)
        {
            e = default;
            if (_next < 0f)
            {
                _next = now + _rng.Range(MinInterval, MaxInterval);
                return false;
            }

            if (now < _next)
                return false;
            _next = now + _rng.Range(MinInterval, MaxInterval);
            var dist = _rng.Range(600f, 1400f);
            e = new DistantEvent
            {
                Kind = DistantKind.Blast,
                Distance = dist,
                AngleDeg = _rng.Range(0f, 360f),
                Volume = DistantBattleScheduler.VolumeFor(DistantKind.Blast, dist),
                LowpassHz = DistantBattleScheduler.LowpassFor(dist),
                Pitch = _rng.Range(0.7f, 0.9f)
            };
            return true;
        }
    }

    /// <summary>Çapraz geçiş (crossfade) matematiği: biyom değişiminde eski yatak söner, sonra yenisi yükselir.</summary>
    public static class BedCrossfade
    {
        /// <summary>Çıkan yatak seviyesi: 1'den 0'a, ilk yarıda söner.</summary>
        public static float Out(float t) => Clamp01(1f - t * 2f);

        /// <summary>Giren yatak seviyesi: ikinci yarıda 0'dan 1'e.</summary>
        public static float In(float t) => Clamp01(t * 2f - 1f);

        /// <summary>Eşit güçlü geçiş kazançları (t: 0..1) — iki yatak aynı anda çalarsa toplam güç sabit.</summary>
        public static void EqualPower(float t, out float outGain, out float inGain)
        {
            t = Clamp01(t);
            outGain = (float)Math.Cos(t * Math.PI * 0.5);
            inGain = (float)Math.Sin(t * Math.PI * 0.5);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
