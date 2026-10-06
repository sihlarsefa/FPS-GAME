using System;
using System.Collections.Generic;

namespace Project.Application.Match.Flow
{
    /// <summary>
    /// Ganimet yoğunluğu dengeleme (saf). Düzgün rastgele dağılım iki hata üretir: yoğun kümelerde
    /// ganimet taşması (herkes iki çanta dolusu silah) ve seyrek bölgelerde "ganimet çölü" (yürüyen oyuncu
    /// 90 sn boyunca hiçbir şey bulamaz). Çözüm: nokta başına çıkma şansını komşu sayısına göre yumuşakça
    /// ölçeklemek — kalabalık kümede kısmak, yalnız noktada hafifçe artırmak. Beklenen eşya toplamı da
    /// oyuncu başına bütçe olarak doğrulanır (PUBG/Warzone ≈ oyuncu başına ≥ 1.5 birincil silah).
    /// </summary>
    public static class LootDensityModel
    {
        public const float DefaultNeighbourRadius = 35f;
        public const float MinMultiplier = 0.65f;
        public const float MaxMultiplier = 1.3f;

        /// <summary>Kademe başına hedef komşu sayısı (yarıçap içinde, kendisi hariç).</summary>
        public static float TargetNeighbours(int tier)
        {
            switch (tier)
            {
                case 0: return 1.5f;
                case 1: return 3f;
                case 2: return 5f;
                default: return 7f;
            }
        }

        /// <summary>Her nokta için yarıçap içindeki diğer nokta sayısı. Izgara ile O(n) civarı.</summary>
        public static int[] CountNeighbours(IReadOnlyList<float> xs, IReadOnlyList<float> zs, float radius)
        {
            var n = xs != null && zs != null ? Math.Min(xs.Count, zs.Count) : 0;
            var counts = new int[n];
            if (n == 0)
                return counts;

            if (!(radius > 0f))
                radius = DefaultNeighbourRadius;

            var cell = radius;
            var grid = new Dictionary<long, List<int>>(n);
            for (var i = 0; i < n; i++)
            {
                var key = CellKey((int)Math.Floor(xs[i] / cell), (int)Math.Floor(zs[i] / cell));
                if (!grid.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>(4);
                    grid[key] = bucket;
                }

                bucket.Add(i);
            }

            var r2 = radius * radius;
            for (var i = 0; i < n; i++)
            {
                var cx = (int)Math.Floor(xs[i] / cell);
                var cz = (int)Math.Floor(zs[i] / cell);
                var c = 0;
                for (var gx = cx - 1; gx <= cx + 1; gx++)
                {
                    for (var gz = cz - 1; gz <= cz + 1; gz++)
                    {
                        if (!grid.TryGetValue(CellKey(gx, gz), out var bucket))
                            continue;

                        for (var k = 0; k < bucket.Count; k++)
                        {
                            var j = bucket[k];
                            if (j == i)
                                continue;
                            var dx = xs[j] - xs[i];
                            var dz = zs[j] - zs[i];
                            if (dx * dx + dz * dz <= r2)
                                c++;
                        }
                    }
                }

                counts[i] = c;
            }

            return counts;
        }

        /// <summary>Şans çarpanı: (hedef / (komşu+1))^0.5, [Min, Max] aralığına kırpılır.</summary>
        public static float ChanceMultiplier(int tier, int neighbours)
        {
            var target = TargetNeighbours(tier) + 1f;
            var ratio = target / (Math.Max(0, neighbours) + 1f);
            var m = (float)Math.Sqrt(ratio);
            return m < MinMultiplier ? MinMultiplier : m > MaxMultiplier ? MaxMultiplier : m;
        }

        public static float AdjustedChance(float baseChance, int tier, int neighbours)
        {
            if (float.IsNaN(baseChance) || float.IsInfinity(baseChance))
                return 0f;
            var c = baseChance * ChanceMultiplier(tier, neighbours);
            return c < 0f ? 0f : c > 1f ? 1f : c;
        }

        /// <summary>Beklenen toplam eşya = Σ düzeltilmiş şans × gruptaki ortalama eşya.</summary>
        public static float ExpectedItems(IReadOnlyList<int> tiers, IReadOnlyList<int> neighbours,
            Func<int, float> baseChanceForTier, float averageGroupSize)
        {
            if (tiers == null || neighbours == null || baseChanceForTier == null)
                return 0f;

            var n = Math.Min(tiers.Count, neighbours.Count);
            var sum = 0f;
            for (var i = 0; i < n; i++)
                sum += AdjustedChance(baseChanceForTier(tiers[i]), tiers[i], neighbours[i]);
            return sum * Math.Max(0f, averageGroupSize);
        }

        /// <summary>Oyuncu başına eşya bütçesi yeterli mi? (varsayılan eşik: oyuncu başına 6 eşya).</summary>
        public static bool MeetsBudget(float expectedItems, int players, float itemsPerPlayer)
        {
            if (players <= 0)
                return true;
            return expectedItems >= players * Math.Max(0f, itemsPerPlayer);
        }

        private static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
