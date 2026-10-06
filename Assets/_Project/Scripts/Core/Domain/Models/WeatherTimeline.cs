using System;
using System.Collections.Generic;

namespace Project.Core.Domain
{
    /// <summary>Zaman çizelgesinin bir andaki hava örneği (hepsi 0..1).</summary>
    public struct WeatherSample
    {
        /// <summary>Bulutluluk (gök kararması, güneş sönmesi).</summary>
        public float Cloud;
        /// <summary>Yağış şiddeti.</summary>
        public float Rain;
        /// <summary>Uzaktaki yağmur perdesinin yaklaşma ilerlemesi (yağmur başlamadan önce 0→1, dinince 1→0).</summary>
        public float Curtain;
        /// <summary>Fırtına (şimşek) olasılığı: yağmurun yoğun evresinde 1.</summary>
        public float Storm;
    }

    /// <summary>Tek şimşek: maç saniyesi, oyuncudan uzaklık (m) ve yön (derece, 0 = +Z).</summary>
    public struct LightningStrike
    {
        public float Time, DistanceM, AzimuthDeg;
        /// <summary>Gök gürültüsünün oyuncuya ulaşma gecikmesi (sn).</summary>
        public float ThunderDelay => WeatherTimeline.ThunderDelaySeconds(DistanceM);
    }

    /// <summary>
    /// Maç tohumundan üretilen deterministik hava çizelgesi (saf, Unity bağımlılığı yok): Açık → bulutlanma (60-120 sn) → yağmur → açılma (60-120 sn).
    /// Sunucu ve istemciler aynı tohum + maç süresiyle aynı örneği hesaplar; ağ üzerinden yalnız tohum gerekir.
    /// </summary>
    public sealed class WeatherTimeline
    {
        public const float SpeedOfSound = 343f;

        private struct Phase { public float Start, End; public int Kind; } // 0 açık, 1 bulutlanma, 2 yağmur, 3 açılma

        private readonly List<Phase> _phases = new List<Phase>();
        private readonly List<LightningStrike> _strikes = new List<LightningStrike>();

        public int Seed { get; }
        public float Horizon { get; }
        public IReadOnlyList<LightningStrike> Strikes => _strikes;
        /// <summary>Yağmurun başladığı ilk an (bulutlanma bitişi); yağmur yoksa -1.</summary>
        public float FirstRainStart { get; private set; } = -1f;

        public static float ThunderDelaySeconds(float distanceM) => distanceM < 0f ? 0f : distanceM / SpeedOfSound;

        public static float Smooth(float x) { x = x < 0f ? 0f : x > 1f ? 1f : x; return x * x * (3f - 2f * x); }

        /// <summary>Gök gürültüsü gürlüğü: yakın = 1, uzak azalır (0.12 tabanı).</summary>
        public static float ThunderVolume(float distanceM)
        {
            float t = distanceM / 3000f; t = t < 0f ? 0f : t > 1f ? 1f : t;
            return 1f - 0.88f * t;
        }

        /// <summary>Şimşek parlaması zarfı (0..1): çift darbe, yaklaşık 0,4 sn.</summary>
        public static float FlashEnvelope(float seconds)
        {
            if (seconds < 0f || seconds > 0.45f) return 0f;
            float a = seconds < 0.06f ? seconds / 0.06f : (float)Math.Exp(-(seconds - 0.06f) * 14f);
            float b = seconds > 0.16f ? (seconds < 0.2f ? (seconds - 0.16f) / 0.04f : (float)Math.Exp(-(seconds - 0.2f) * 10f)) * 0.7f : 0f;
            float v = Math.Max(a, b);
            return v > 1f ? 1f : v;
        }

        public WeatherTimeline(int seed, float horizonSeconds = 3600f)
        {
            Seed = seed;
            Horizon = horizonSeconds < 60f ? 60f : horizonSeconds;
            Build();
        }

        // xorshift32: platformdan bağımsız deterministik üretici.
        private static uint Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        private static float Range(ref uint s, float lo, float hi) => lo + (Next(ref s) & 0xFFFFFF) / 16777216f * (hi - lo);

        private void Build()
        {
            uint s = (uint)Seed * 2654435761u + 0x9E3779B9u;
            if (s == 0) s = 1;
            for (int i = 0; i < 4; i++) Next(ref s);

            float t = Range(ref s, 120f, 420f); // açık hava ile başlar
            int cycles = 0;
            while (t < Horizon && cycles < 4)
            {
                float cloud = Range(ref s, 60f, 120f);
                float rain = Range(ref s, 240f, 600f);
                float clear = Range(ref s, 60f, 120f);
                _phases.Add(new Phase { Start = t, End = t + cloud, Kind = 1 });
                float rs = t + cloud;
                if (FirstRainStart < 0f) FirstRainStart = rs;
                _phases.Add(new Phase { Start = rs, End = rs + rain, Kind = 2 });
                _phases.Add(new Phase { Start = rs + rain, End = rs + rain + clear, Kind = 3 });
                AddStrikes(ref s, rs + 20f, rs + rain - 20f);
                t = rs + rain + clear + Range(ref s, 300f, 900f);
                cycles++;
            }
        }

        private void AddStrikes(ref uint s, float from, float to)
        {
            float t = from + Range(ref s, 5f, 25f);
            while (t < to)
            {
                float d = Next(ref s) % 5u == 0u ? Range(ref s, 120f, 600f) : Range(ref s, 600f, 3000f);
                _strikes.Add(new LightningStrike { Time = t, DistanceM = d, AzimuthDeg = Range(ref s, 0f, 360f) });
                t += Range(ref s, 8f, 28f);
            }
        }

        /// <summary>Maç saniyesi t için hava örneği.</summary>
        public WeatherSample Sample(float t)
        {
            var r = new WeatherSample();
            for (int i = 0; i < _phases.Count; i++)
            {
                var p = _phases[i];
                if (t < p.Start) break;
                float u = (t - p.Start) / Math.Max(0.001f, p.End - p.Start);
                bool inside = t < p.End;
                switch (p.Kind)
                {
                    case 1:
                        if (inside) { float k = Smooth(u); r.Cloud = k; r.Curtain = k; r.Rain = 0f; r.Storm = 0f; }
                        break;
                    case 2:
                        if (inside)
                        {
                            r.Cloud = 1f; r.Curtain = 1f;
                            r.Rain = Smooth(Math.Min(1f, (t - p.Start) / 25f)); // ilk 25 sn'de şiddetlenir
                            r.Storm = Smooth(Math.Min(1f, (t - p.Start) / 60f));
                        }
                        break;
                    case 3:
                        if (inside)
                        {
                            float rainFade = Smooth(Math.Min(1f, u * 2f)); // yağmur ilk yarıda diner
                            r.Rain = 1f - rainFade; r.Storm = Math.Max(0f, 1f - u * 3f);
                            r.Cloud = 1f - Smooth(u);
                            r.Curtain = r.Rain > 0.001f ? 1f : 1f - Smooth(Math.Max(0f, (u - 0.5f) * 2f));
                        }
                        break;
                }
            }
            return r;
        }

        /// <summary>[from, to) aralığındaki şimşekleri döndürür (sıralı).</summary>
        public void StrikesBetween(float from, float to, List<LightningStrike> into)
        {
            if (into == null) return;
            for (int i = 0; i < _strikes.Count; i++)
            {
                float st = _strikes[i].Time;
                if (st >= to) break;
                if (st >= from) into.Add(_strikes[i]);
            }
        }

        /// <summary>Yağış eşiği: histerezisle Açık/Yağmur ayrık türü (titreşimi önler).</summary>
        public static WeatherKind KindFor(float rain, WeatherKind previous)
        {
            if (previous == WeatherKind.Yagmur) return rain < 0.12f ? WeatherKind.Acik : WeatherKind.Yagmur;
            return rain > 0.3f ? WeatherKind.Yagmur : WeatherKind.Acik;
        }

        /// <summary>Hava durumuna göre rüzgâr gücü (WindRules ölçeği 0..1,5: açık 0,35 → fırtına ~1,1).</summary>
        public static float WindStrength(WeatherSample s) => 0.35f + 0.3f * s.Cloud + 0.35f * s.Rain + 0.15f * s.Storm;
    }
}
