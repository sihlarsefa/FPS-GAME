using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Savaş yorgunu iç mekan: saf sayı/olasılık mantığı (kurşun deliği, cam kırığı, enkaz, asker eşyası, ışık huzmesi, devrik mobilya).
    /// Kademe 0..3 = QualitySettings.GetQualityLevel(); yoğunluk kademeyle artar. Çizim: BuildingGeneratorInterior.
    /// </summary>
    public static class InteriorWearPlan
    {
        public static int Tier() => Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);

        public static float Density(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 0.3f;
                case 1: return 0.65f;
                case 2: return 1f;
                default: return 1.4f;
            }
        }

        private static float RuinMul(bool ruined) => ruined ? 1.6f : 1f;

        /// <summary>Bir duvar yüzü için kurşun deliği sayısı (iç yüz, metre başına ~0,22).</summary>
        public static int BulletHoles(float wallLength, int tier, bool ruined)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, wallLength) * 0.22f * Density(tier) * RuinMul(ruined)));

        /// <summary>Pencere başına zemindeki cam kırığı parçası.</summary>
        public static int Shards(int tier, bool ruined) => Mathf.RoundToInt(6f * Density(tier) * RuinMul(ruined));

        /// <summary>Oda başına yerdeki enkaz parçası (sıva/tuğla kırıntısı).</summary>
        public static int Debris(float area, int tier, bool ruined)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp(area / 5f, 0f, 8f) * Density(tier) * RuinMul(ruined)));

        /// <summary>Oda başına terk edilmiş asker eşyası (boş şarjör, telsiz, matara, kovan).</summary>
        public static int SoldierItems(float area, int tier, bool ruined)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp(area / 9f, 0f, 3f) * Density(tier) * (ruined ? 1.3f : 1f)));

        /// <summary>Bina başına en çok ışık huzmesi.</summary>
        public static int MaxShafts(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 0;
                case 1: return 2;
                case 2: return 4;
                default: return 8;
            }
        }

        /// <summary>Oda başına en çok mobilya parçası (GameObject maliyetini kademeye bağlar).</summary>
        public static int MaxPiecesPerRoom(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 3;
                case 1: return 5;
                case 2: return 7;
                default: return 9;
            }
        }

        /// <summary>Pervaz saksısı + yırtık perde olasılığı (yaşanmışlık; yıkıkta perde daha sık yırtık, saksı yok).</summary>
        public static float CurtainChance(int tier) => Mathf.Clamp01(0.2f + 0.2f * Mathf.Clamp(tier, 0, 3));

        /// <summary>Sandalye/masa devrik olasılığı.</summary>
        public static float TipChance(int tier, bool ruined) => Mathf.Clamp01((0.18f + 0.06f * Mathf.Clamp(tier, 0, 3)) * (ruined ? 1.8f : 1f));

        /// <summary>Yıkık binada parçanın tümden atılma olasılığı.</summary>
        public static float RuinedDropChance => 0.25f;

        public static Vector2 RandomIn(Rect r, System.Random rng, float inset)
        {
            var x0 = r.xMin + inset;
            var x1 = r.xMax - inset;
            var z0 = r.yMin + inset;
            var z1 = r.yMax - inset;
            if (x1 < x0) x0 = x1 = r.center.x;
            if (z1 < z0) z0 = z1 = r.center.y;
            return new Vector2(x0 + (x1 - x0) * (float)rng.NextDouble(), z0 + (z1 - z0) * (float)rng.NextDouble());
        }
    }
}
