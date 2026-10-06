using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Arazi ayrıntı (çimen/çiçek) katmanları.</summary>
    public enum DetailKind
    {
        Grass = 0,
        DryGrass = 1,
        FlowerWhite = 2,
        FlowerYellow = 3,
        FlowerViolet = 4,
        /// <summary>Hasat sonrası kısa anız (tarla parselleri).</summary>
        Stubble = 5
    }

    /// <summary>
    /// Arazi DETAIL katmanları: prosedürel çimen tutamı dokulu DetailPrototype'lar ve alphamap'ten türetilen yoğunluk haritaları.
    /// Platoda çimen / kuru çimen / çiçek; yol, kaya, kar, çakıl ve dik yamaçlarda yok. Yoğunluk kalite kademesine göre
    /// <c>Terrain.detailObjectDensity</c> ile (bkz. <c>PerformanceProfile.DetailDensityScale</c>) ve mesafe ile ölçeklenir.
    /// Rüzgâr: TerrainData.wavingGrass* ayarları.
    /// </summary>
    public static class VegetationPainter
    {
        public const int KindCount = 5;

        /// <summary>Anız dahil toplam ayrıntı türü (prototip sayısı).</summary>
        public const int TotalKindCount = 6;
        public const int PatchSize = 16;

        /// <summary>Hücre başına azami örnek sayısı (tür sırasıyla).</summary>
        public static readonly int[] MaxPerCell = { 9, 5, 2, 2, 2, 4 };

        /// <summary>Pürüzsüz doğrusal 0..1 yardımcı.</summary>
        private static float Smooth(float a, float b, float x)
        {
            var t = Mathf.Clamp01((x - a) / Mathf.Max(1e-5f, b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Saf yoğunluk kuralı. weights: alphamap ağırlıkları (TerrainLayerKind sırası, uzunluk ≥ 8); slopeDeg eğim; aboveWater metre;
        /// patch01 / cluster01: 0..1 gürültüler. density: 0..1 (KindCount uzunluğunda) doldurulur.
        /// </summary>
        public static void ComputeDensities(float[] weights, float slopeDeg, float aboveWater, float patch01, float cluster01, float[] density)
        {
            for (var i = 0; i < density.Length; i++)
                density[i] = 0f;

            var grass = weights[(int)TerrainLayerKind.Grass];
            var dry = weights[(int)TerrainLayerKind.DryGrass];
            var blockers = weights[(int)TerrainLayerKind.Rock] + weights[(int)TerrainLayerKind.Snow] + weights[(int)TerrainLayerKind.Asphalt]
                           + weights[(int)TerrainLayerKind.Gravel] * 0.9f + weights[(int)TerrainLayerKind.Mud] * 0.8f
                           + weights[(int)TerrainLayerKind.Dirt] * 0.55f;
            var free = Mathf.Clamp01(1f - blockers * 1.6f);
            var slopeFree = 1f - Smooth(24f, 38f, slopeDeg);
            var shore = Smooth(0.15f, 0.9f, aboveWater);
            var gate = free * slopeFree * shore;
            if (gate <= 0.02f)
                return;

            // Çimen: yeşil katmanda, yama yama seyrekleşir.
            density[(int)DetailKind.Grass] = Mathf.Clamp01(Mathf.Pow(grass, 0.9f) * (0.5f + 0.5f * patch01) * gate * 1.15f);
            // Kuru çimen: kuru katmanda + çimenin seyrek kenarlarında.
            density[(int)DetailKind.DryGrass] = Mathf.Clamp01((dry * 0.95f + grass * 0.18f * (1f - patch01)) * (0.4f + 0.6f * patch01) * gate);

            // Çiçek: sadece çimen/kuru çimen ağırlıklı, hafif eğimli yerlerde; kümeler halinde (cluster01 yüksekse).
            var meadow = Mathf.Clamp01((grass + dry) * 1.1f - 0.3f);
            var flowerGate = meadow * gate * (1f - Smooth(12f, 22f, slopeDeg));
            var cluster = Smooth(0.7f, 0.9f, cluster01);
            if (cluster > 0f)
            {
                var kind = cluster01 < 0.78f ? DetailKind.FlowerWhite : cluster01 < 0.84f ? DetailKind.FlowerYellow : DetailKind.FlowerViolet;
                density[(int)kind] = Mathf.Clamp01(cluster * flowerGate);
            }
        }

        /// <summary>Yoğunluk (0..1) → hücre başına örnek sayısı (tür azami değerine göre).</summary>
        public static int ToCount(DetailKind kind, float density)
        {
            var max = MaxPerCell[(int)kind];
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(density) * max), 0, max);
        }

        // ------------------------------------------------------------------ Prototipler ve uygulama

        /// <summary>5 ayrıntı prototipi (çimen, kuru çimen, 3 çiçek). Dokular prosedüreldir (tohuma bağlı).</summary>
        public static DetailPrototype[] CreatePrototypes(int seed)
        {
            var grassTex = VegetationTextures.CreateGrassTuft(seed + 1);
            var dryTex = VegetationTextures.CreateGrassTuft(seed + 2);
            var flowerTex = VegetationTextures.CreateFlower(seed + 3);
            return new[]
            {
                Billboard(grassTex, new Color(0.62f, 0.78f, 0.34f), new Color(0.72f, 0.7f, 0.38f), 0.9f, 1.5f, 0.55f, 1.0f),
                Billboard(dryTex, new Color(0.82f, 0.74f, 0.42f), new Color(0.66f, 0.56f, 0.3f), 1.0f, 1.7f, 0.6f, 1.15f),
                Billboard(flowerTex, new Color(1f, 1f, 1f), new Color(0.92f, 0.92f, 0.86f), 0.5f, 0.8f, 0.4f, 0.7f),
                Billboard(flowerTex, new Color(1f, 0.88f, 0.25f), new Color(0.95f, 0.75f, 0.2f), 0.5f, 0.8f, 0.4f, 0.7f),
                Billboard(flowerTex, new Color(0.65f, 0.45f, 0.95f), new Color(0.55f, 0.35f, 0.8f), 0.5f, 0.8f, 0.4f, 0.7f),
                Billboard(dryTex, new Color(0.86f, 0.76f, 0.46f), new Color(0.7f, 0.58f, 0.32f), 0.45f, 0.75f, 0.22f, 0.4f)
            };
        }

        private static DetailPrototype Billboard(Texture2D tex, Color healthy, Color dry, float minW, float maxW, float minH, float maxH)
        {
            return new DetailPrototype
            {
                prototypeTexture = tex,
                usePrototypeMesh = false,
                renderMode = DetailRenderMode.GrassBillboard,
                healthyColor = healthy,
                dryColor = dry,
                minWidth = minW,
                maxWidth = maxW,
                minHeight = minH,
                maxHeight = maxH,
                noiseSpread = 0.25f,
                holeEdgePadding = 0.25f
            };
        }

        /// <summary>Rüzgâr: waving grass ayarları (yumuşak, düşük genlik).</summary>
        public static void ApplyWind(TerrainData data)
        {
            data.wavingGrassStrength = 0.35f;
            data.wavingGrassSpeed = 0.45f;
            data.wavingGrassAmount = 0.5f;
            data.wavingGrassTint = new Color(0.78f, 0.82f, 0.68f, 1f);
        }

        /// <summary>
        /// Ayrıntı katmanlarını üretir ve TerrainData'ya yazar. alphamaps: [z, x, katman] (TerrainPainter çıktısı; null ise yapılmaz).
        /// Çözünürlük = alphamap çözünürlüğü (kare). Üretilen prototipleri döner.
        /// </summary>
        public static DetailPrototype[] Apply(TerrainData data, TerrainModel model, float[,,] alphamaps, int seed)
        {
            if (data == null || model == null || alphamaps == null)
                return null;

            var res = alphamaps.GetLength(0);
            if (res < PatchSize || alphamaps.GetLength(1) != res || alphamaps.GetLength(2) < TerrainTextureFactory.LayerCount)
                return null;

            var prototypes = CreatePrototypes(seed);
            data.detailPrototypes = prototypes;
            data.SetDetailResolution(res, PatchSize);
            ApplyWind(data);

            var noise = model.Noise;
            var water = model.Layout.WaterLevel;
            var step = model.Size / (res - 1);
            var layers = new int[TotalKindCount][,];
            for (var k = 0; k < TotalKindCount; k++)
                layers[k] = new int[res, res];
            var plan = FieldPlan.Of(model);

            var w = new float[TerrainTextureFactory.LayerCount];
            var density = new float[TotalKindCount];
            for (var z = 0; z < res; z++)
            {
                var wz = model.OriginZ + z * step;
                for (var x = 0; x < res; x++)
                {
                    var wx = model.OriginX + x * step;
                    for (var l = 0; l < w.Length; l++)
                        w[l] = alphamaps[z, x, l];
                    var slope = model.SampleSlope(wx, wz);
                    var above = model.SampleHeight(wx, wz) - water;
                    var patch = noise.Fbm(wx / 14f + 31.7f, wz / 14f - 12.3f, 2) * 0.5f + 0.5f;
                    var cluster = noise.Fbm(wx / 9f - 5.1f, wz / 9f + 77.7f, 2) * 0.5f + 0.5f;
                    ComputeDensities(w, slope, above, Mathf.Clamp01(patch), Mathf.Clamp01(cluster), density);
                    if (plan != null)
                    {
                        var parcel = plan.Find(wx, wz, out var inside);
                        var fieldW = parcel != null ? TerrainNoise.SmoothStep(-0.4f, 1.2f, inside) : 0f;
                        var ridge = 1f;
                        if (parcel != null)
                        {
                            parcel.ToLocal(wx, wz, out var lx, out _);
                            ridge = FieldDetailRules.RowRidge(lx);
                        }

                        FieldDetailRules.ModulateDensity(density, fieldW, parcel != null ? parcel.Kind : FieldKind.Fallow, ridge,
                            plan.TrampleAt(wx, wz), model.SampleRoadEdge(wx, wz), Mathf.Clamp01(cluster));
                    }

                    for (var k = 0; k < TotalKindCount; k++)
                        layers[k][z, x] = ToCount((DetailKind)k, density[k]);
                }
            }

            for (var k = 0; k < TotalKindCount; k++)
                data.SetDetailLayer(0, 0, k, layers[k]);
            return prototypes;
        }

        /// <summary>Yapı sınırları (XZ, margin kadar genişletilmiş) içindeki çimen/çiçekleri siler. Silinen hücre sayısını döner.</summary>
        public static int RemoveDetailsInBounds(Terrain terrain, IReadOnlyList<Bounds> bounds, float margin)
        {
            if (terrain == null || terrain.terrainData == null || bounds == null || bounds.Count == 0)
                return 0;
            var data = terrain.terrainData;
            var res = data.detailResolution;
            if (res <= 0 || data.detailPrototypes == null || data.detailPrototypes.Length == 0)
                return 0;

            var size = data.size;
            var origin = terrain.transform.position;
            var removed = 0;
            for (var i = 0; i < bounds.Count; i++)
            {
                var b = bounds[i];
                int x0, x1, z0, z1;
                if (!CellRange(b, margin, origin, size, res, out x0, out x1, out z0, out z1))
                    continue;
                var w = x1 - x0 + 1;
                var h = z1 - z0 + 1;
                for (var k = 0; k < data.detailPrototypes.Length; k++)
                {
                    var layer = data.GetDetailLayer(x0, z0, w, h, k);
                    var changed = false;
                    for (var z = 0; z < h; z++)
                    {
                        for (var x = 0; x < w; x++)
                        {
                            if (layer[z, x] == 0)
                                continue;
                            layer[z, x] = 0;
                            removed++;
                            changed = true;
                        }
                    }

                    if (changed)
                        data.SetDetailLayer(x0, z0, k, layer);
                }
            }

            return removed;
        }

        /// <summary>Sınır kutusunun (margin ile) kapsadığı ayrıntı hücre aralığı (kapsayıcı); kutu arazi dışındaysa false.</summary>
        public static bool CellRange(Bounds b, float margin, Vector3 terrainOrigin, Vector3 terrainSize, int res,
            out int x0, out int x1, out int z0, out int z1)
        {
            var fx0 = (b.min.x - margin - terrainOrigin.x) / terrainSize.x;
            var fx1 = (b.max.x + margin - terrainOrigin.x) / terrainSize.x;
            var fz0 = (b.min.z - margin - terrainOrigin.z) / terrainSize.z;
            var fz1 = (b.max.z + margin - terrainOrigin.z) / terrainSize.z;
            x0 = z0 = x1 = z1 = 0;
            if (fx1 < 0f || fz1 < 0f || fx0 > 1f || fz0 > 1f)
                return false;
            x0 = Mathf.Clamp(Mathf.FloorToInt(fx0 * res), 0, res - 1);
            x1 = Mathf.Clamp(Mathf.CeilToInt(fx1 * res), 0, res - 1);
            z0 = Mathf.Clamp(Mathf.FloorToInt(fz0 * res), 0, res - 1);
            z1 = Mathf.Clamp(Mathf.CeilToInt(fz1 * res), 0, res - 1);
            return x1 >= x0 && z1 >= z0;
        }
    }
}
