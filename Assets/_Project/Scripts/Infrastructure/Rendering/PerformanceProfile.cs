using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Kalite kademesi (0 Düşük … 3 Ultra) başına performans tablosu: katman bazlı çizim mesafesi,
    /// arazi ağaç/ayrıntı mesafeleri ve gölge ayarları. Saf veri + küçük uygulayıcı; Docs/PERFORMANS.md ile aynıdır.
    /// </summary>
    public static class PerformanceProfile
    {
        public const int MinLevel = 0;
        public const int MaxLevel = 3;

        // Düşük, Orta, Yüksek, Ultra
        private static readonly float[] SmallPropCull = { 90f, 140f, 200f, 300f };   // Loot / küçük eşya
        private static readonly float[] BotCull = { 220f, 320f, 450f, 600f };        // Bot / Player / Hitbox çizimi
        private static readonly float[] TreeDistance = { 450f, 700f, 900f, 1100f };
        private static readonly float[] BillboardStart = { 90f, 130f, 170f, 220f };
        private static readonly float[] DetailDistance = { 0f, 40f, 70f, 100f };
        private static readonly float[] DetailDensity = { 0f, 0.4f, 0.75f, 1f };
        private static readonly int[] MaxFullLodTrees = { 800, 1500, 2500, 4000 };
        private static readonly int[] PixelLights = { 2, 4, 6, 8 };
        private static readonly int[] MaxMuzzleLights = { 2, 4, 6, 8 };

        public static int Clamp(int level) => Mathf.Clamp(level, MinLevel, MaxLevel);
        public static float SmallPropCullDistance(int level) => SmallPropCull[Clamp(level)];
        public static float BotCullDistance(int level) => BotCull[Clamp(level)];
        public static float TreeDrawDistance(int level) => TreeDistance[Clamp(level)];
        public static float TreeBillboardStart(int level) => BillboardStart[Clamp(level)];
        public static float DetailObjectDistance(int level) => DetailDistance[Clamp(level)];
        public static float DetailDensityScale(int level) => DetailDensity[Clamp(level)];
        public static int FullLodTreeCount(int level) => MaxFullLodTrees[Clamp(level)];
        public static int AdditionalLightLimit(int level) => PixelLights[Clamp(level)];
        public static int ActiveFlashLightLimit(int level) => MaxMuzzleLights[Clamp(level)];

        /// <summary>Katman başına çizim mesafesi dizisini (32 eleman; 0 = kamera uzak kırpma) doldurur.</summary>
        public static void FillLayerCullDistances(int level, float[] distances)
        {
            if (distances == null || distances.Length < 32)
                return;
            for (var i = 0; i < 32; i++)
                distances[i] = 0f;
            distances[GameLayersLoot] = SmallPropCullDistance(level);
            distances[GameLayersBot] = BotCullDistance(level);
            distances[GameLayersPlayer] = BotCullDistance(level);
            distances[GameLayersProjectile] = SmallPropCullDistance(level);
        }

        private const int GameLayersLoot = GameLayers.Loot;
        private const int GameLayersBot = GameLayers.Bot;
        private const int GameLayersPlayer = GameLayers.Player;
        private const int GameLayersProjectile = GameLayers.Projectile;

        /// <summary>Kameraya katman kırpma mesafelerini uygular (null güvenli).</summary>
        public static void ApplyToCamera(Camera camera, int level)
        {
            if (camera == null)
                return;
            var distances = new float[32];
            FillLayerCullDistances(level, distances);
            camera.layerCullDistances = distances;
        }

        /// <summary>Render hattı kademesini (STP/render scale/GRD/gölge/mip streaming) etkin URP varlığına uygular.</summary>
        public static void ApplyPipeline(int level)
        {
            PipelineTiers.ApplyRuntime(RenderPipelineInfo.UrpAsset, level);
            StpCrashGuard.DisableStpOnActivePipeline();
        }

        /// <summary>Arazi ağaç/ayrıntı mesafelerini kalite kademesine göre uygular (null güvenli).</summary>
        public static void ApplyToTerrains(int level)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains == null)
                return;
            for (var i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null)
                    continue;
                t.treeDistance = TreeDrawDistance(level);
                t.treeBillboardDistance = TreeBillboardStart(level);
                t.treeMaximumFullLODCount = FullLodTreeCount(level);
                // ≥800 m: 1 km haritada uzak zemin düşük çözünürlüklü basemap'e erken düşüp bulanıklaşmasın.
                t.basemapDistance = Mathf.Max(800f, Project.Infrastructure.World.TerrainPaintRules.BasemapDistance(level));
                t.detailObjectDistance = DetailObjectDistance(level);
                // GrassSystem etkinken ayrıntı yoğunluğu 0 tutulur (GrassSystem 128 karede bir sıfırlar); yeniden açma.
                if (!Project.Infrastructure.World.GrassSystem.Active)
                    t.detailObjectDensity = DetailDensityScale(level);
            }
        }
    }
}
