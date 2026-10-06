using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Silah malzemesi saf matematiği (Unity bağımsız, test edilebilir): namlu karbon birikimi/temizlenmesi, kenar aşınması maskesi
    /// (gölgelendirici ile aynı formül), viewmodel taban ışığı. Gölgelendirici HarekatGunLitPass.hlsl bu değerleri yeniden uygular.
    /// </summary>
    public static class WeaponWearMath
    {
        /// <summary>Atış başına karbon artışı (0-1 ölçeğinde). Büyük kalibre / uzun namlu daha çok kurum bırakır.</summary>
        public static float CarbonPerShot(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return 0.0035f;
                case WeaponCategory.Smg: return 0.0045f;
                case WeaponCategory.AssaultRifle: return 0.006f;
                case WeaponCategory.Dmr: return 0.008f;
                case WeaponCategory.Lmg: return 0.0055f;
                case WeaponCategory.Shotgun: return 0.012f;
                case WeaponCategory.Sniper: return 0.014f;
                default: return 0.005f;
            }
        }

        /// <summary>Bir atıştan sonra karbon: doygunluğa yaklaştıkça artış yavaşlar (kurum üstüne kurum az tutar).</summary>
        public static float AddShot(float carbon, float perShot)
        {
            carbon = Clamp01(carbon);
            var next = carbon + Math.Max(0f, perShot) * (1f - 0.6f * carbon);
            return Clamp01(next);
        }

        /// <summary>Zamanla (bakım/temizlik) karbon azalır; saniyede varsayılan %0,25 (yaklaşık 6-7 dk'da tam temiz).</summary>
        public static float Clean(float carbon, float seconds, float perSecond = 0.0025f)
        {
            if (seconds <= 0f || perSecond <= 0f)
                return Clamp01(carbon);
            return Clamp01(carbon - perSecond * seconds);
        }

        /// <summary>Namlu ucundan geriye karbon bölgesi uzunluğu (m): tabancada kısa, tüfekte uzun.</summary>
        public static float CarbonZoneLength(float barrelLength)
        {
            return Math.Max(0.04f, Math.Min(0.22f, barrelLength * 0.35f));
        }

        /// <summary>Gölgelendirici <c>smoothstep(muzzleZ-len, muzzleZ+0.01, z)</c> ile aynı: namlu ucuna doğru 0→1.</summary>
        public static float MuzzleMask(float z, float muzzleZ, float length)
        {
            var len = Math.Max(0.02f, length);
            return SmoothStep(muzzleZ - len, muzzleZ + 0.01f, z);
        }

        /// <summary>Karbon sürtünmesi yağ parlaklığını bastırır: 0 karbon = 1, tam karbon = 0,5.</summary>
        public static float SheenMultiplier(float carbon) => 1f - 0.5f * Clamp01(carbon);

        /// <summary>Gölgelendirici kenar aşınması: kenara uzaklık (m), genişlik (m), 0-1 gürültü (çentik).</summary>
        public static float EdgeMask(float edgeDistance, float width, float noise)
        {
            var w = Math.Max(0.0002f, width) * (0.45f + 1.7f * Clamp01(noise));
            return 1f - SmoothStep(0f, w, edgeDistance);
        }

        /// <summary>Kenar maskesi x aşınma alanı; 1.9 çarpanı gölgelendiriciyle aynıdır.</summary>
        public static float EdgeWear(float edgeMask, float wearAmount, float fieldNoise)
        {
            var field = Clamp01(wearAmount * (0.3f + 1.1f * Clamp01(fieldNoise)));
            return Clamp01(edgeMask * field * 1.9f);
        }

        /// <summary>
        /// Viewmodel taban ışığı eki: ortam parlaklığı hedefin altındaysa (iç mekân/gece) aradaki fark kadar (en çok maxBias) eklenir.
        /// Silah düz siyah kalmasın; açık havada 0.
        /// </summary>
        public static float FillBias(float ambientLuminance, float targetLuminance = 0.12f, float maxBias = 0.1f)
        {
            if (float.IsNaN(ambientLuminance))
                ambientLuminance = 0f;
            return Math.Min(Math.Max(0f, maxBias), Math.Max(0f, targetLuminance - ambientLuminance) * 0.75f);
        }

        /// <summary>Rec.709 parlaklık.</summary>
        public static float Luminance(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;

        internal static float Clamp01(float v) => float.IsNaN(v) ? 0f : (v < 0f ? 0f : (v > 1f ? 1f : v));

        internal static float SmoothStep(float a, float b, float x)
        {
            if (b <= a)
                return x >= b ? 1f : 0f;
            var t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// Silah başına kalıcı (oturum boyu) namlu karbon durumu. Zaman parametre olarak verilir (test edilebilir).
    /// Aktif olmayan silah da zamanla temizlenir (bakım varsayımı).
    /// </summary>
    public static class WeaponCarbonState
    {
        private struct Entry
        {
            public float Carbon;
            public float Time;
        }

        private static readonly Dictionary<string, Entry> Map = new Dictionary<string, Entry>(16);

        /// <summary>Her atışta/temizlikte artar; sürücüler değişimi bununla anlar.</summary>
        public static int Version { get; private set; }

        public static float Get(string weaponId, float now)
        {
            if (string.IsNullOrEmpty(weaponId) || !Map.TryGetValue(weaponId, out var e))
                return 0f;
            return WeaponWearMath.Clean(e.Carbon, now - e.Time);
        }

        public static float RegisterShot(string weaponId, WeaponCategory category, float now)
        {
            if (string.IsNullOrEmpty(weaponId))
                return 0f;
            var current = Get(weaponId, now);
            var next = WeaponWearMath.AddShot(current, WeaponWearMath.CarbonPerShot(category));
            Map[weaponId] = new Entry { Carbon = next, Time = now };
            Version++;
            return next;
        }

        /// <summary>Tüm silahları tertemiz yapar (maç başı).</summary>
        public static void Reset()
        {
            Map.Clear();
            Version++;
        }
    }
}
