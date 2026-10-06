using System;
using Project.Infrastructure.Vfx;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>Ayak sesi yüzeyi (ses karakteri); Vfx.SurfaceKind'den eşlenir.</summary>
    public enum FootSurface { Concrete = 0, Dirt, Gravel, Grass, Wood, Metal, Snow, Water, Foliage }

    /// <summary>Yürüyüş biçimi: sessizlik ve menzil bunlara göre değişir.</summary>
    public enum Gait { Prone = 0, CrouchWalk, Walk, Jog, Sprint }

    /// <summary>Yüzeyin ayak sesi özellikleri.</summary>
    public readonly struct SurfaceProfile
    {
        /// <summary>Yürüme hızında 1 m'deki ek seviye (dB, betona göre).</summary>
        public readonly float LevelOffsetDb;
        /// <summary>Topuk vuruşunun parlaklığı (alçak geçiren kesim Hz).</summary>
        public readonly float BrightnessHz;
        /// <summary>Yapı sesi (üst/alt kata geçer): ahşap/metal 1, beton 0.2.</summary>
        public readonly float StructureBorne;
        /// <summary>Islaklıkta ek sıçrama katmanı duyarlılığı (0..1).</summary>
        public readonly float WetSensitivity;
        /// <summary>Sürtünme/çatırdama (gravel, kar, yaprak) gürültü payı 0..1.</summary>
        public readonly float Crunch;

        public SurfaceProfile(float level, float brightness, float structure, float wet, float crunch)
        {
            LevelOffsetDb = level;
            BrightnessHz = brightness;
            StructureBorne = structure;
            WetSensitivity = wet;
            Crunch = crunch;
        }
    }

    /// <summary>Tek ayak vuruşu planı: topuk + ayak parmağı çift fazı.</summary>
    public readonly struct FootfallPlan
    {
        public readonly float HeelGain;
        public readonly float ToeGain;
        public readonly float ToeDelaySeconds;
        public readonly float Pitch;
        public readonly float CutoffHz;
        public readonly float LevelDb;
        public readonly float SplashGain;
        public readonly float CrunchGain;

        public FootfallPlan(float heel, float toe, float toeDelay, float pitch, float cutoff, float level, float splash, float crunch)
        {
            HeelGain = heel;
            ToeGain = toe;
            ToeDelaySeconds = toeDelay;
            Pitch = pitch;
            CutoffHz = cutoff;
            LevelDb = level;
            SplashGain = splash;
            CrunchGain = crunch;
        }
    }

    /// <summary>
    /// Ayak sesi gerçekçiliği (saf): yüzey × yürüyüş hızı × yük kombinasyonu seviyesi, adım uzunluğu, topuk-parmak fazı,
    /// ıslaklık, kademeli duyulma menzili. Tarkov tarzı: sessiz yürüyüş (çömelme/yavaş) gerçekten kısa menzilli,
    /// koşu metalde/çakılda çok uzağa duyulur; PUBG tarzı ses ipuçları için seviye kuyu tanımlıdır.
    /// </summary>
    public static class FootstepRules
    {
        /// <summary>Yürüme, beton, 80 kg: 1 m'de oyun ölçeği seviye (dB).</summary>
        public const float WalkConcreteDb = 70f;
        /// <summary>Kaynak ses seviyesi için tam birim ses (1.0) referansı (dB).</summary>
        public const float FullVolumeDb = 80f;
        public const float BodyMassKg = 80f;

        public static FootSurface FromSurfaceKind(SurfaceKind k)
        {
            switch (k)
            {
                case SurfaceKind.Dirt: return FootSurface.Dirt;
                case SurfaceKind.Concrete: return FootSurface.Concrete;
                case SurfaceKind.Metal: return FootSurface.Metal;
                case SurfaceKind.Wood: return FootSurface.Wood;
                case SurfaceKind.Flesh: return FootSurface.Dirt;
                case SurfaceKind.Water: return FootSurface.Water;
                case SurfaceKind.Foliage: return FootSurface.Foliage;
                case SurfaceKind.Snow: return FootSurface.Snow;
                default: return FootSurface.Concrete;
            }
        }

        public static SurfaceProfile Profile(FootSurface s)
        {
            switch (s)
            {
                case FootSurface.Dirt: return new SurfaceProfile(-2f, 3200f, 0.15f, 0.5f, 0.2f);
                case FootSurface.Gravel: return new SurfaceProfile(5f, 7000f, 0.2f, 0.3f, 1f);
                case FootSurface.Grass: return new SurfaceProfile(-4f, 2600f, 0.1f, 0.6f, 0.25f);
                case FootSurface.Wood: return new SurfaceProfile(2f, 4800f, 0.9f, 0.2f, 0.1f);
                case FootSurface.Metal: return new SurfaceProfile(4f, 9000f, 1f, 0.15f, 0f);
                case FootSurface.Snow: return new SurfaceProfile(-6f, 2000f, 0.05f, 0.1f, 0.7f);
                case FootSurface.Water: return new SurfaceProfile(3f, 5500f, 0.05f, 1f, 0.2f);
                case FootSurface.Foliage: return new SurfaceProfile(-1f, 5200f, 0.05f, 0.4f, 0.9f);
                default: return new SurfaceProfile(0f, 6500f, 0.2f, 0.4f, 0.05f);
            }
        }

        /// <summary>Hızdan yürüyüş biçimi. prone/crouch bayrakları önceliklidir. Eşikler: 1.2 / 3.2 / 4.8 m/s.</summary>
        public static Gait GaitFor(float horizontalSpeed, bool crouching, bool prone)
        {
            if (prone) return Gait.Prone;
            if (crouching) return Gait.CrouchWalk;
            if (horizontalSpeed < 3.2f) return Gait.Walk;
            if (horizontalSpeed < 4.8f) return Gait.Jog;
            return Gait.Sprint;
        }

        /// <summary>Yürüyüş biçiminin seviye farkı (dB, yürüyüşe göre).</summary>
        public static float GaitDb(Gait g)
        {
            switch (g)
            {
                case Gait.Prone: return -18f;
                case Gait.CrouchWalk: return -10f;
                case Gait.Jog: return 4f;
                case Gait.Sprint: return 9f;
                default: return 0f;
            }
        }

        /// <summary>Adım uzunluğu (m): hızla büyür; çömelmede kısa, koşuda uzun.</summary>
        public static float StrideLength(Gait g)
        {
            switch (g)
            {
                case Gait.Prone: return 0.35f;
                case Gait.CrouchWalk: return 0.5f;
                case Gait.Jog: return 1.05f;
                case Gait.Sprint: return 1.55f;
                default: return 0.75f;
            }
        }

        /// <summary>Yük (silah+yelek+çanta kg): seviye 20*log10((gövde+yük)/gövde).</summary>
        public static float LoadDb(float gearKg)
        {
            var g = gearKg < 0f ? 0f : gearKg > 60f ? 60f : gearKg;
            return 20f * (float)Math.Log10((BodyMassKg + g) / BodyMassKg);
        }

        /// <summary>1 m'deki ayak sesi seviyesi (dB). Islaklık (0..1) duyarlı yüzeylerde +dB.</summary>
        public static float LevelDb(FootSurface s, Gait g, float gearKg, float wetness01 = 0f)
        {
            var prof = Profile(s);
            var w = wetness01 < 0f ? 0f : wetness01 > 1f ? 1f : wetness01;
            return WalkConcreteDb + prof.LevelOffsetDb + GaitDb(g) + LoadDb(gearKg) + 4f * w * prof.WetSensitivity;
        }

        /// <summary>AudioSource ses (0..1) = 10^((seviye-80)/20), kırpılmış.</summary>
        public static float SourceVolume(float levelDb)
        {
            var v = (float)Math.Pow(10.0, (levelDb - FullVolumeDb) / 20.0);
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>
        /// Duyulma menzili (m) = 10^((seviye - eşik)/20) / soğurma düzeltmesi. Eşik = ortam + <paramref name="marginDb"/> (12 dB).
        /// Referans: yürüme ~14 m, jog ~22 m, koşu ~40 m, çömelme ~4 m, sürünme ~2 m (beton, 35 dB ortam, 0 kg ek yük).
        /// </summary>
        public static float AudibleRange(float levelDb, float ambientDb = 35f, float marginDb = 12f, float maxRange = 90f)
        {
            var excess = levelDb - (ambientDb + marginDb);
            if (excess <= 0f) return 0f;
            var d = (float)Math.Pow(10.0, excess / 20.0);
            return d > maxRange ? maxRange : d;
        }

        /// <summary>Topuk/parmak planı: hız arttıkça parmak vuruşu zayıflar ve gecikmesi kısalır (ön ayak koşusu).</summary>
        public static FootfallPlan Plan(FootSurface s, Gait g, float speed, float gearKg, float wetness01, bool leftFoot)
        {
            var prof = Profile(s);
            var level = LevelDb(s, g, gearKg, wetness01);
            var t = (speed - 0.5f) / 5f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var toeGain = g == Gait.Sprint ? 0.2f : 0.55f - 0.25f * t;
            if (g == Gait.Prone) toeGain = 0.1f;
            var toeDelay = g == Gait.Prone ? 0.02f : 0.11f - 0.07f * t;
            var heel = g == Gait.Sprint ? 0.9f : 1f;
            // Sol/sağ ayak: algısal ayrım için ±%2 perde farkı.
            var pitch = leftFoot ? 0.98f : 1.02f;
            if (g == Gait.Sprint) pitch *= 1.03f; else if (g == Gait.Prone || g == Gait.CrouchWalk) pitch *= 0.97f;
            var w = wetness01 < 0f ? 0f : wetness01 > 1f ? 1f : wetness01;
            var splash = s == FootSurface.Water ? 1f : prof.WetSensitivity * w * 0.6f;
            // Parlaklık: ıslak yüzeyde kesim düşer (su filmi sönümler); hızla hafif artar.
            var cutoff = prof.BrightnessHz * (1f - 0.25f * w * prof.WetSensitivity) * (0.9f + 0.2f * t);
            var crunch = prof.Crunch * (0.4f + 0.6f * t);
            return new FootfallPlan(heel, toeGain, toeDelay, pitch, cutoff, level, splash, crunch);
        }
    }
}
