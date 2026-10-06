using Project.Core.Domain;
using Project.Infrastructure.Audio;
using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Gece savaş görselleri için saf kurallar (Unity gerektirmez, test edilebilir): iz mermisi seçimi, HDR parlaklık,
    /// namlu ışığı kademe sınırı ve titreşimi, patlama ışığı gece çarpanı, sis ışık saçılımı.
    /// </summary>
    public static class VfxNightRules
    {
        /// <summary>Her 3. mermi iz bırakır (makineli tüfekte hepsi).</summary>
        public const int TracerEvery = 3;

        /// <summary>0 = gündüz, 1 = gece. Şafak/akşam arası değerler.</summary>
        public static float NightFactor(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Safak: return 0.4f;
                case TimeOfDay.Aksam: return 0.6f;
                case TimeOfDay.Gece: return 1f;
                default: return 0f;
            }
        }

        /// <summary>İz çizilsin mi? Sayaç her atışta artar; makineli tüfekte hep, diğerlerinde her 3. atışta.</summary>
        public static bool ShouldDrawTracer(int shotCounter, CaliberClass caliber)
        {
            if (caliber == CaliberClass.MachineGun)
                return true;
            if (caliber == CaliberClass.Explosion)
                return false;
            return shotCounter % TracerEvery == 0;
        }

        /// <summary>Bloom dostu HDR çarpanı: gündüz 1.6, gece 6.</summary>
        public static float TracerHdr(float night)
        {
            return Mathf.Lerp(1.6f, 6f, Mathf.Clamp01(night));
        }

        /// <summary>Kalibreye göre iz kalınlığı (m).</summary>
        public static float TracerWidth(CaliberClass caliber)
        {
            switch (caliber)
            {
                case CaliberClass.Pistol: return 0.014f;
                case CaliberClass.MachineGun: return 0.03f;
                case CaliberClass.Sniper: return 0.034f;
                default: return 0.022f;
            }
        }

        /// <summary>Kalibreye göre iz rengi: makineli tüfek kırmızı-turuncu, keskin nişancı beyaza yakın, diğerleri sarı-turuncu.</summary>
        public static Color TracerTint(CaliberClass caliber)
        {
            switch (caliber)
            {
                case CaliberClass.MachineGun: return new Color(1f, 0.38f, 0.16f, 1f);
                case CaliberClass.Sniper: return new Color(1f, 0.92f, 0.78f, 1f);
                default: return new Color(1f, 0.74f, 0.32f, 1f);
            }
        }

        /// <summary>Tek karede çizilen iz parçasının uzunluğu (m): hıza göre, 6-60 m.</summary>
        public static float TracerLength(float speed, float seconds)
        {
            if (float.IsNaN(speed) || speed <= 0f)
                return 6f;
            return Mathf.Clamp(speed * Mathf.Max(0.005f, seconds), 6f, 60f);
        }

        /// <summary>Aynı anda yanabilecek namlu ışığı: Düşük 1, Orta 2, Yüksek 3.</summary>
        public static int MuzzleLightCap(VfxTier tier)
        {
            return tier == VfxTier.High ? 3 : tier == VfxTier.Medium ? 2 : 1;
        }

        /// <summary>Namlu ışığı şiddet çarpanı: gece daha belirgin (x1.8), 0.75-1.25 titreşim.</summary>
        public static float MuzzleLightScale(float night, float flicker01)
        {
            return Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(night)) * Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(flicker01));
        }

        /// <summary>Patlama ışığı şiddet/menzil çarpanı: gece 2.2 / 1.5.</summary>
        public static float ExplosionIntensityScale(float night)
        {
            return Mathf.Lerp(1f, 2.2f, Mathf.Clamp01(night));
        }

        public static float ExplosionRangeScale(float night)
        {
            return Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(night));
        }

        /// <summary>
        /// Sis içi ışık saçılımı: parlama sis bulutunun (yarıçap + 6 m) içindeyse parlaklık 0..1; kenara doğru ve uzakta söner,
        /// gece daha güçlü. Dışında 0.
        /// </summary>
        public static float SmokeGlow(float distanceToCenter, float cloudRadius, float flashStrength01, float night)
        {
            if (cloudRadius <= 0f || distanceToCenter < 0f)
                return 0f;
            var reach = cloudRadius + 6f;
            if (distanceToCenter >= reach)
                return 0f;
            var falloff = 1f - distanceToCenter / reach;
            return Mathf.Clamp01(falloff * Mathf.Clamp01(flashStrength01) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(night)));
        }
    }
}
