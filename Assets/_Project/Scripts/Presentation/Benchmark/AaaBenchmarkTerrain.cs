using System.Collections.Generic;
using Project.Infrastructure;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.Benchmark
{
    /// <summary>
    /// 150×150 m benchmark arazisi: TerrainData'yı <see cref="BenchmarkLayout"/> ile doldurur; mevcut TerrainTextureFactory
    /// katmanları (çamur, kaya, çim...), TreeFactory ağaç prototipleri, VegetationPainter çimen prototipleri ve RockFactory
    /// kayaları kullanılır. Yalnız genel API; eksik/hatalı parça sahnenin geri kalanını durdurmaz.
    /// </summary>
    public static class AaaBenchmarkTerrain
    {
        public const int HeightmapResolution = 257;
        public const int AlphamapResolution = 256;
        public const int DetailResolution = 256;

        public sealed class Result
        {
            public Terrain Terrain;
            public GameObject Root;
            public int TreeCount;
            public int RockCount;
        }

        public static Result Build(Transform parent, int seed, int qualityTier)
        {
            var result = new Result();
            var root = new GameObject("[Benchmark Arazi]");
            if (parent != null)
                root.transform.SetParent(parent, false);
            result.Root = root;

            var data = new TerrainData { name = "HK_AaaBenchmark_TerrainData" };
            data.heightmapResolution = HeightmapResolution;
            data.size = new Vector3(BenchmarkLayout.Size, BenchmarkLayout.TerrainHeight, BenchmarkLayout.Size);
            ApplyHeights(data);

            var holder = new GameObject("[Ağaç Prototipleri]");
            holder.transform.SetParent(root.transform, false);
            holder.transform.position = new Vector3(0f, -2000f, 0f);

            TerrainLayer[] layers = null;
            try
            {
                layers = TerrainTextureFactory.CreateLayers(seed, 512);
                data.terrainLayers = layers;
                data.alphamapResolution = AlphamapResolution;
                data.baseMapResolution = 512;
                var alpha = BuildAlphamaps(data);
                data.SetAlphamaps(0, 0, alpha);
                TryDetails(data, alpha, seed);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Arazi katmanları: " + e.Message);
            }

            try
            {
                var prototypes = TreeFactory.CreateDefaultPrototypes(holder.transform, seed);
                data.treePrototypes = TreeFactory.ToTreePrototypes(prototypes);
                data.RefreshPrototypes();
                var trees = ScatterTrees(data.size, seed, qualityTier);
                data.SetTreeInstances(trees, false);
                result.TreeCount = trees.Length;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Ağaçlar: " + e.Message);
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = TerrainGenerator.TerrainObjectName;
            go.layer = GameLayers.Default;
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(-BenchmarkLayout.HalfSize, 0f, -BenchmarkLayout.HalfSize);
            var terrain = go.GetComponent<Terrain>();
            result.Terrain = terrain;
            if (terrain != null)
            {
                terrain.allowAutoConnect = false;
                terrain.heightmapPixelError = 3f;
                terrain.drawInstanced = true;
                terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
                terrain.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                terrain.drawTreesAndFoliage = true;
                terrain.treeCrossFadeLength = 8f;
                var material = MaterialLibrary.TerrainMaterial;
                if (material != null)
                    terrain.materialTemplate = material;
                ApplyTier(terrain, qualityTier);
            }

            var collider = go.GetComponent<TerrainCollider>();
            if (collider != null)
                collider.terrainData = data;

            result.RockCount = ScatterRocks(root.transform, terrain, seed);
            return result;
        }

        /// <summary>Kalite kademesini terrain ayarlarına uygular (çim mesafesi/yoğunluğu, ağaç mesafeleri).</summary>
        public static void ApplyTier(Terrain terrain, int tier)
        {
            if (terrain == null)
                return;
            TerrainPaintRules.ApplyTier(terrain, tier);
            TreeFactory.ApplyTier(terrain, tier);
            terrain.treeDistance = PerformanceProfile.TreeDrawDistance(tier);
            terrain.treeMaximumFullLODCount = PerformanceProfile.FullLodTreeCount(tier);
        }

        private static void ApplyHeights(TerrainData data)
        {
            var res = HeightmapResolution;
            var heights = new float[res, res];
            var step = BenchmarkLayout.Size / (res - 1);
            var inv = 1f / BenchmarkLayout.TerrainHeight;
            for (var z = 0; z < res; z++)
            {
                var wz = -BenchmarkLayout.HalfSize + z * step;
                for (var x = 0; x < res; x++)
                {
                    var wx = -BenchmarkLayout.HalfSize + x * step;
                    heights[z, x] = Mathf.Clamp01(BenchmarkLayout.HeightAt(wx, wz) * inv);
                }
            }

            data.SetHeights(0, 0, heights);
        }

        private static float[,,] BuildAlphamaps(TerrainData data)
        {
            var res = AlphamapResolution;
            var maps = new float[res, res, BenchmarkLayout.LayerCount];
            var w = new float[BenchmarkLayout.LayerCount];
            var step = BenchmarkLayout.Size / (res - 1);
            for (var z = 0; z < res; z++)
            {
                var wz = -BenchmarkLayout.HalfSize + z * step;
                for (var x = 0; x < res; x++)
                {
                    var wx = -BenchmarkLayout.HalfSize + x * step;
                    var slope = SlopeDegrees(wx, wz);
                    BenchmarkLayout.Weights(wx, wz, slope, w);
                    for (var l = 0; l < BenchmarkLayout.LayerCount; l++)
                        maps[z, x, l] = w[l];
                }
            }

            return maps;
        }

        public static float SlopeDegrees(float x, float z)
        {
            const float d = 0.6f;
            var dx = (BenchmarkLayout.HeightAt(x + d, z) - BenchmarkLayout.HeightAt(x - d, z)) / (2f * d);
            var dz = (BenchmarkLayout.HeightAt(x, z + d) - BenchmarkLayout.HeightAt(x, z - d)) / (2f * d);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        private static void TryDetails(TerrainData data, float[,,] alpha, int seed)
        {
            try
            {
                var prototypes = VegetationPainter.CreatePrototypes(seed);
                data.detailPrototypes = prototypes;
                data.SetDetailResolution(DetailResolution, VegetationPainter.PatchSize);
                VegetationPainter.ApplyWind(data);

                var res = DetailResolution;
                var layersByKind = new int[VegetationPainter.KindCount][,];
                for (var k = 0; k < layersByKind.Length; k++)
                    layersByKind[k] = new int[res, res];

                var aRes = alpha.GetLength(0);
                var step = BenchmarkLayout.Size / (res - 1);
                for (var z = 0; z < res; z++)
                {
                    var wz = -BenchmarkLayout.HalfSize + z * step;
                    var az = Mathf.Clamp(Mathf.RoundToInt(z / (float)(res - 1) * (aRes - 1)), 0, aRes - 1);
                    for (var x = 0; x < res; x++)
                    {
                        var wx = -BenchmarkLayout.HalfSize + x * step;
                        var ax = Mathf.Clamp(Mathf.RoundToInt(x / (float)(res - 1) * (aRes - 1)), 0, aRes - 1);
                        var grass = alpha[az, ax, BenchmarkLayout.LGrass];
                        var dry = alpha[az, ax, BenchmarkLayout.LDry];
                        var patch = Mathf.PerlinNoise((wx + 12f) * 0.35f, (wz - 8f) * 0.35f);
                        var flower = Mathf.PerlinNoise((wx - 40f) * 0.6f, (wz + 70f) * 0.6f) > 0.78f ? 1f : 0f;
                        layersByKind[0][z, x] = VegetationPainter.ToCount(DetailKind.Grass, grass * (0.55f + 0.45f * patch) * 1.15f);
                        layersByKind[1][z, x] = VegetationPainter.ToCount(DetailKind.DryGrass, dry * (0.4f + 0.6f * patch));
                        layersByKind[2][z, x] = VegetationPainter.ToCount(DetailKind.FlowerWhite, grass * flower * 0.5f);
                        layersByKind[3][z, x] = VegetationPainter.ToCount(DetailKind.FlowerYellow, grass * flower * 0.4f * (1f - patch));
                        layersByKind[4][z, x] = 0;
                    }
                }

                for (var k = 0; k < layersByKind.Length; k++)
                    data.SetDetailLayer(0, 0, k, layersByKind[k]);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Çimen katmanı: " + e.Message);
            }
        }

        private static TreeInstance[] ScatterTrees(Vector3 size, int seed, int tier)
        {
            var rng = new System.Random(seed * 31 + 7);
            var target = tier >= 2 ? 520 : tier == 1 ? 320 : 180;
            var list = new List<TreeInstance>(target + 16);
            var attempts = target * 12;
            for (var a = 0; a < attempts && list.Count < target; a++)
            {
                var x = (float)(rng.NextDouble() * 2.0 - 1.0) * (BenchmarkLayout.HalfSize - 3f);
                var z = (float)(rng.NextDouble() * 2.0 - 1.0) * (BenchmarkLayout.HalfSize - 3f);
                if ((float)rng.NextDouble() > BenchmarkLayout.TreeDensity(x, z))
                    continue;

                var roll = (float)rng.NextDouble();
                var kind = roll < 0.38f ? TreeKind.PineA : roll < 0.62f ? TreeKind.PineB : roll < 0.78f ? TreeKind.Oak
                    : roll < 0.84f ? TreeKind.Dead : TreeKind.Bush;
                var h = Range(rng, 0.8f, 1.25f);
                var n = BenchmarkLayout.ToNormalized(x, z);
                list.Add(new TreeInstance
                {
                    prototypeIndex = (int)kind,
                    position = new Vector3(n.x, Mathf.Clamp01(BenchmarkLayout.HeightAt(x, z) / size.y), n.y),
                    heightScale = h,
                    widthScale = h * Range(rng, 0.9f, 1.1f),
                    rotation = Range(rng, 0f, Mathf.PI * 2f),
                    color = Color.Lerp(Color.white, new Color(0.85f, 0.9f, 0.8f), (float)rng.NextDouble()),
                    lightmapColor = Color.white
                });
            }

            return list.ToArray();
        }

        private static int ScatterRocks(Transform parent, Terrain terrain, int seed)
        {
            var holder = new GameObject("Kayalar");
            holder.transform.SetParent(parent, false);
            var rng = new System.Random(seed * 17 + 3);
            var count = 0;
            for (var a = 0; a < 700 && count < 70; a++)
            {
                var x = (float)(rng.NextDouble() * 2.0 - 1.0) * (BenchmarkLayout.HalfSize - 4f);
                var z = (float)(rng.NextDouble() * 2.0 - 1.0) * (BenchmarkLayout.HalfSize - 4f);
                if (BenchmarkLayout.PadWeight(x, z) > 0.05f)
                    continue;
                var rockM = BenchmarkLayout.Membership(BenchmarkLayout.RockZone, x, z, 0.5f);
                var mudM = BenchmarkLayout.Membership(BenchmarkLayout.MudZone, x, z, 0.5f);
                var chance = 0.04f + rockM * 0.9f + mudM * 0.25f;
                if ((float)rng.NextDouble() > chance)
                    continue;

                var y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : BenchmarkLayout.HeightAt(x, z);
                var size = Range(rng, 0.6f, 1.7f) * ((float)rng.NextDouble() < 0.15f ? Range(rng, 1.6f, 2.4f) : 1f);
                var scale = new Vector3(size, size * Range(rng, 0.7f, 1.1f), size * Range(rng, 0.8f, 1.2f));
                var rotation = Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, 0f, 360f), Range(rng, -8f, 8f));
                RockFactory.CreateRock(holder.transform, new Vector3(x, y + 0.25f * scale.y, z), rotation, scale, rng.Next(0, RockFactory.VariantCount), rockM > 0.5f);
                count++;
            }

            return count;
        }

        private static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();
    }
}
