using System;
using Project.Core.Domain;
using Project.Infrastructure.Vfx;

namespace Project.Infrastructure.Audio
{
    /// <summary>Ses sınıfı (loudness hedefi için).</summary>
    public enum LoudnessClass { Pistol, Smg, Rifle, Dmr, Lmg, Sniper, Shotgun, Suppressed, Impact }

    /// <summary>
    /// Saf (Unity'siz) yüksek ses ölçümü: tepe dBFS ve yaklaşık LUFS (tek atımlık klipler için K-ağırlıklı yaklaşım:
    /// 100 Hz tek kutuplu yüksek geçiren + ağırlıksız ortalama kare). Tavan: tüm ateş katmanları en çok -1 dBFS.
    /// </summary>
    public static class LoudnessMath
    {
        public const float PeakCeilingDb = -1f;
        public static readonly float PeakCeilingLinear = (float)Math.Pow(10.0, PeakCeilingDb / 20.0);
        public const float SilenceDb = -120f;
        public const float ToleranceLu = 3f;

        public static float ToDb(float linear) => linear <= 1e-6f ? SilenceDb : 20f * (float)Math.Log10(linear);
        public static float FromDb(float db) => (float)Math.Pow(10.0, db / 20.0);

        public static float PeakLinear(float[] s)
        {
            if (s == null) return 0f;
            var m = 0f;
            for (var i = 0; i < s.Length; i++)
            {
                var a = Math.Abs(s[i]);
                if (a > m) m = a;
            }
            return m;
        }

        public static float PeakDb(float[] s) => ToDb(PeakLinear(s));

        /// <summary>Atışın "gürültülü" kısmının yaklaşık LUFS değeri: en güçlü <paramref name="windowSeconds"/> penceresinin ortalaması.</summary>
        public static float ApproxLufs(float[] s, int sampleRate = 44100, float windowSeconds = 0.4f)
        {
            if (s == null || s.Length == 0 || sampleRate <= 0) return SilenceDb;
            var a = (float)Math.Exp(-2.0 * Math.PI * 100.0 / sampleRate);
            var win = Math.Max(1, Math.Min(s.Length, (int)(windowSeconds * sampleRate)));
            var sq = new double[s.Length];
            double lp = 0;
            for (var i = 0; i < s.Length; i++)
            {
                lp = a * lp + (1 - a) * s[i];
                var y = s[i] - lp;
                sq[i] = y * y;
            }

            double sum = 0, best = 0;
            for (var i = 0; i < s.Length; i++)
            {
                sum += sq[i];
                if (i >= win) sum -= sq[i - win];
                if (i >= win - 1 && sum > best) best = sum;
            }

            if (best <= 0) return SilenceDb;
            return (float)(10.0 * Math.Log10(best / win)) - 0.691f;
        }

        /// <summary>Sınıfın hedef LUFS değeri (ateş sınıfları arasında tutarlı; susturuculu belirgin kısık).</summary>
        public static float TargetLufs(LoudnessClass c)
        {
            switch (c)
            {
                case LoudnessClass.Pistol: return -22f;
                case LoudnessClass.Smg: return -21f;
                case LoudnessClass.Rifle: return -20f;
                case LoudnessClass.Dmr: return -19f;
                case LoudnessClass.Lmg: return -19f;
                case LoudnessClass.Sniper: return -18f;
                case LoudnessClass.Shotgun: return -19f;
                case LoudnessClass.Suppressed: return -28f;
                default: return -26f;
            }
        }

        public static LoudnessClass ClassOf(WeaponCategory cat, bool suppressed)
        {
            if (suppressed) return LoudnessClass.Suppressed;
            switch (cat)
            {
                case WeaponCategory.Pistol: return LoudnessClass.Pistol;
                case WeaponCategory.Smg: return LoudnessClass.Smg;
                case WeaponCategory.Dmr: return LoudnessClass.Dmr;
                case WeaponCategory.Sniper: return LoudnessClass.Sniper;
                case WeaponCategory.Shotgun: return LoudnessClass.Shotgun;
                case WeaponCategory.Lmg: return LoudnessClass.Lmg;
                default: return LoudnessClass.Rifle;
            }
        }

        /// <summary>
        /// Klibi hedef LUFS'a getirecek doğrusal kazanç; kazancı -1 dBFS tepe tavanı ve ±<paramref name="maxDb"/> ile sınırlar.
        /// </summary>
        public static float GainToTarget(float measuredLufs, float peakLinear, float targetLufs, float maxDb = 12f)
        {
            if (measuredLufs <= SilenceDb + 1f || peakLinear <= 1e-6f) return 1f;
            var db = Math.Max(-maxDb, Math.Min(maxDb, targetLufs - measuredLufs));
            var g = FromDb(db);
            var maxGain = PeakCeilingLinear / peakLinear;
            return Math.Min(g, maxGain);
        }

        public static bool WithinTolerance(float measuredLufs, float targetLufs, float toleranceLu = ToleranceLu) =>
            Math.Abs(measuredLufs - targetLufs) <= toleranceLu;

        /// <summary>İç mekân atışı: ortam kuyruğu ve ateş katmanı seçimi (Indoor -> tail_indoor).</summary>
        public static Foley.FireLayer TailLayerFor(AudioEnvironment env) =>
            env == AudioEnvironment.Indoor ? Foley.FireLayer.TailIndoor
            : env == AudioEnvironment.Valley ? Foley.FireLayer.TailValley
            : Foley.FireLayer.TailOutdoor;

        public static SoundId TailSoundFor(AudioEnvironment env) =>
            env == AudioEnvironment.Indoor ? SoundId.ShotTailIndoor
            : env == AudioEnvironment.Valley ? SoundId.ShotTailValley
            : SoundId.ShotTailOutdoor;
    }

    /// <summary>Yüzey türüne göre mermi isabet sesi (gerçek klip: <c>Resources/Audio/SFX/Impact/&lt;yüzey&gt;/impact_n.wav</c>, yoksa prosedürel).</summary>
    public static class SurfaceImpactAudio
    {
        public const string Root = "Audio/SFX/Impact";

        public static SoundId SoundFor(SurfaceKind k)
        {
            switch (k)
            {
                case SurfaceKind.Metal: return SoundId.BulletImpactMetal;
                case SurfaceKind.Dirt: return SoundId.BulletImpactDirt;
                case SurfaceKind.Wood: return SoundId.BulletImpactWood;
                case SurfaceKind.Flesh: return SoundId.BulletImpactFlesh;
                case SurfaceKind.Water: return SoundId.BulletImpactWater;
                case SurfaceKind.Snow: return SoundId.BulletImpactSnow;
                case SurfaceKind.Foliage: return SoundId.BulletImpactFoliage;
                default: return SoundId.BulletImpact;
            }
        }

        public static string FolderFor(SurfaceKind k) => Root + "/" + k.ToString().ToLowerInvariant();

        /// <summary>Yüzeye göre ses çarpanı (yumuşak yüzeyler daha kısık).</summary>
        public static float VolumeScale(SurfaceKind k)
        {
            switch (k)
            {
                case SurfaceKind.Metal: return 1f;
                case SurfaceKind.Concrete: return 1f;
                case SurfaceKind.Wood: return 0.9f;
                case SurfaceKind.Flesh: return 0.8f;
                case SurfaceKind.Water: return 0.85f;
                case SurfaceKind.Dirt: return 0.75f;
                case SurfaceKind.Snow: return 0.6f;
                case SurfaceKind.Foliage: return 0.55f;
                default: return 0.9f;
            }
        }
    }
}
