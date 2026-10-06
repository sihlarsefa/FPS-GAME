using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Bulut gölgesi kademe ayarı (0 Düşük: kapalı).</summary>
    public readonly struct CloudShadowTierConfig
    {
        public readonly bool Enabled;
        public readonly int TextureSize;
        public readonly int Octaves;
        /// <summary>Çerez (cookie) kenar uzunluğu, metre. Doku bu aralıkta bir kez tekrarlanır.</summary>
        public readonly float CookieSize;

        public CloudShadowTierConfig(bool enabled, int textureSize, int octaves, float cookieSize)
        {
            Enabled = enabled; TextureSize = textureSize; Octaves = octaves; CookieSize = cookieSize;
        }
    }

    /// <summary>Bulut gölgesi saf matematiği (ana ışık çerezi): kademe tablosu, hava başına kapsama/derinlik, rüzgârla kayma.</summary>
    public static class CloudShadowsMath
    {
        public const float CookieSizeMeters = 800f;

        public static CloudShadowTierConfig ForTier(int tier)
        {
            if (tier <= 0) return new CloudShadowTierConfig(false, 0, 0, CookieSizeMeters);
            if (tier == 1) return new CloudShadowTierConfig(true, 128, 3, CookieSizeMeters);
            if (tier == 2) return new CloudShadowTierConfig(true, 256, 4, CookieSizeMeters);
            return new CloudShadowTierConfig(true, 256, 5, CookieSizeMeters);
        }

        /// <summary>Gece ana ışık ay olduğundan bulut gölgesi yok.</summary>
        public static bool IsActive(TimeOfDay time, int tier) => tier > 0 && time != TimeOfDay.Gece;

        /// <summary>Gölge kapsaması 0..1 (gökyüzü bulut kapsamasından biraz seyrek: parçalı bulut gölgeleri okunur kalsın).</summary>
        public static float Coverage(TimeOfDay time, WeatherKind weather)
        {
            float c;
            switch (weather)
            {
                case WeatherKind.Yagmur: c = 0.80f; break;
                case WeatherKind.Kar: c = 0.70f; break;
                default: c = 0.36f; break;
            }

            if (weather == WeatherKind.Acik && (time == TimeOfDay.Safak || time == TimeOfDay.Aksam))
                c += 0.06f;
            return Mathf.Clamp01(c);
        }

        /// <summary>Gölge derinliği 0..1 (kapalı havada güneş zaten yayılmış: gölge sığ).</summary>
        public static float Darkness(TimeOfDay time, WeatherKind weather)
        {
            if (weather == WeatherKind.Yagmur) return 0.28f;
            if (weather == WeatherKind.Kar) return 0.32f;
            if (time == TimeOfDay.Safak || time == TimeOfDay.Aksam) return 0.5f;
            return 0.62f;
        }

        /// <summary>Çerez değeri: 1 = tam ışık, 1-derinlik = bulut gölgesi çekirdeği.</summary>
        public static float CookieValue(float density, float coverage, float darkness)
        {
            float a = SkyWaterRules.CloudAlpha(density, coverage);
            return 1f - Mathf.Clamp01(darkness) * a;
        }

        /// <summary>Bulut gölgesi hızı (m/sn): rüzgâr gücü 0..1.5 -> 3..14.</summary>
        public static float SpeedMps(float windStrength)
        {
            float s = Mathf.Clamp01(windStrength / 1.5f);
            return Mathf.Lerp(3f, 14f, s);
        }

        public static float Wrap(float v, float size)
        {
            if (size <= 0f) return 0f;
            float r = v % size;
            return r < 0f ? r + size : r;
        }

        /// <summary>
        /// Çerez ofsetini rüzgâr yönünde ilerletir. URP: UV = (ışık uzayı konumu - ofset) / boyut, yani desen +ofset yönünde kayar;
        /// ışık uzayı ofseti = dünya yer değiştirmesinin ışığın right/up eksenlerine izdüşümü. Ofset boyut aralığında sarılır (doku tekrarlı).
        /// </summary>
        public static Vector2 AdvanceOffset(Vector2 offset, float windDx, float windDz, float speedMps, float dt, Vector3 lightRight, Vector3 lightUp, float cookieSize)
        {
            float dx = windDx * speedMps * dt;
            float dz = windDz * speedMps * dt;
            float ox = offset.x + dx * lightRight.x + dz * lightRight.z;
            float oy = offset.y + dx * lightUp.x + dz * lightUp.z;
            return new Vector2(Wrap(ox, cookieSize), Wrap(oy, cookieSize));
        }
    }
}
