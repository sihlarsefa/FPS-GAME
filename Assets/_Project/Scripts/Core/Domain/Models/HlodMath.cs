using System;

namespace Project.Core.Domain
{
    /// <summary>
    /// S6: HLOD / doku akışı / arazi karo planı için saf matematik (Unity bağımlılığı yok; EditMode testli).
    /// Kademe: 0 Düşük, 1 Orta, 2 Yüksek, 3 Ultra.
    /// </summary>
    public static class HlodMath
    {
        // Yakın -> uzak proxy geçiş mesafesi (m). Düşük kademede daha erken geçilir (daha ucuz).
        private static readonly float[] SwapDistances = { 120f, 160f, 220f, 300f };
        // Doku akışı bütçesi (MB), azami mip düşürme, kare başına işlenen renderer sayısı.
        private static readonly int[] StreamBudgetMb = { 384, 640, 1024, 1536 };
        private static readonly int[] StreamMaxReduction = { 3, 2, 1, 1 };
        private static readonly int[] StreamRenderersPerFrame = { 128, 256, 512, 512 };

        public static int ClampTier(int tier) => tier < 0 ? 0 : (tier > 3 ? 3 : tier);

        public static float SwapDistance(int tier) => SwapDistances[ClampTier(tier)];

        /// <summary>Histerezis payı: mesafenin %10'u, en az 8 m.</summary>
        public static float Hysteresis(float swapDistance) => Math.Max(8f, swapDistance * 0.10f);

        /// <summary>
        /// Proxy kullanılmalı mı? Yakınken proxy'ye ancak (swap + h) üstünde; proxy'deyken ancak (swap - h) altında geri döner.
        /// </summary>
        public static bool ShouldUseProxy(bool currentlyProxy, float distance, float swapDistance)
        {
            var h = Hysteresis(swapDistance);
            return currentlyProxy ? distance > swapDistance - h : distance > swapDistance + h;
        }

        /// <summary>Parça büyüklüğü (en büyük kenar) alt sınırın altındaysa proxy'den atılır.</summary>
        public static bool KeepPart(float sizeX, float sizeY, float sizeZ, float minExtent)
            => Math.Max(sizeX, Math.Max(sizeY, sizeZ)) >= minExtent;

        /// <summary>
        /// Proxy parça seçimi: küçük parçaları at, kalanları büyükten küçüğe üçgen bütçesi dolana dek al.
        /// maxExtents[i] = parçanın en büyük kenarı, tris[i] = üçgen sayısı. triBudget &lt;= 0 sınırsız.
        /// </summary>
        public static bool[] SelectParts(float[] maxExtents, int[] tris, float minExtent, int triBudget)
        {
            if (maxExtents == null || tris == null) return new bool[0];
            var n = Math.Min(maxExtents.Length, tris.Length);
            var keep = new bool[n];
            var order = new int[n];
            for (var i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) => maxExtents[b].CompareTo(maxExtents[a]));
            long used = 0;
            for (var k = 0; k < n; k++)
            {
                var i = order[k];
                if (maxExtents[i] < minExtent) continue;
                if (triBudget > 0 && used + tris[i] > triBudget) continue;
                keep[i] = true;
                used += tris[i];
            }
            return keep;
        }

        /// <summary>Birleşik mesh 16 bit indekse sığar mı (65535 köşe)?</summary>
        public static bool FitsUInt16(long vertexCount) => vertexCount <= 65535;

        // ---- Doku akışı ----

        public static int StreamingBudgetMb(int tier) => StreamBudgetMb[ClampTier(tier)];
        public static int StreamingMaxLevelReduction(int tier) => StreamMaxReduction[ClampTier(tier)];
        public static int StreamingRenderersPerFrame(int tier) => StreamRenderersPerFrame[ClampTier(tier)];

        /// <summary>Kademe bütçesi, ekran kartı belleğinin %35'ini aşmaz (4 GB RTX3050 -> ~1400 MB); en az 128 MB.</summary>
        public static int EffectiveStreamingBudgetMb(int tier, int graphicsMemoryMb)
        {
            var b = StreamingBudgetMb(tier);
            if (graphicsMemoryMb > 0)
                b = Math.Min(b, graphicsMemoryMb * 35 / 100);
            return Math.Max(128, b);
        }

        // ---- Occlusion bake parametreleri ----
        public const float OcclusionSmallestOccluder = 5f;   // m: bundan küçük nesne örtücü sayılmaz
        public const float OcclusionSmallestHole = 0.25f;    // m: kamera geçebilir en küçük boşluk
        public const float OcclusionBackfaceThreshold = 100f; // %: arka yüz hücreleri atılmaz (güvenli varsayılan)

        /// <summary>Haritaya göre occluder boyutu: büyük haritada daha iri (bake süresi + bellek).</summary>
        public static float SmallestOccluderForMap(float mapSizeMeters)
            => mapSizeMeters >= 4000f ? 8f : (mapSizeMeters >= 1500f ? 5f : 3f);

        // ---- Arazi karo planı (8-10 km) ----

        public static int TileCount(float mapSizeMeters, float tileSizeMeters)
        {
            if (mapSizeMeters <= 0f || tileSizeMeters <= 0f) return 1;
            return Math.Max(1, (int)Math.Ceiling(mapSizeMeters / tileSizeMeters));
        }

        public static int TileIndex1D(float worldCoord, float tileSizeMeters, int tilesPerAxis)
        {
            if (tileSizeMeters <= 0f || tilesPerAxis <= 0) return 0;
            var i = (int)Math.Floor(worldCoord / tileSizeMeters);
            return i < 0 ? 0 : (i >= tilesPerAxis ? tilesPerAxis - 1 : i);
        }

        /// <summary>Kameradan yarıçap içinde (yükleme yarıçapı) kalan karo sayısı üst sınırı: (2*ceil(r/tile)+1)^2.</summary>
        public static int TilesInRadius(float radiusMeters, float tileSizeMeters)
        {
            if (tileSizeMeters <= 0f) return 1;
            var k = (int)Math.Ceiling(Math.Max(0f, radiusMeters) / tileSizeMeters);
            return (2 * k + 1) * (2 * k + 1);
        }

        /// <summary>Karo yükleme histerezisi: yükleme r, boşaltma r + tile/2.</summary>
        public static bool TileShouldBeLoaded(bool loaded, float distanceToTileCenter, float loadRadius, float tileSizeMeters)
            => loaded ? distanceToTileCenter <= loadRadius + tileSizeMeters * 0.5f : distanceToTileCenter <= loadRadius;
    }
}
