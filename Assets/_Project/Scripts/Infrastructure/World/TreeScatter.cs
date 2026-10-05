using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Arazi ağacı yerleşimi: yoğunluk alanı (Çam Sırtı ormanı, kenar sırtları, vadi meşelikleri, dere kıyısı çalılıkları,
    /// harabe çevresinde kuru ağaçlar) × seyreltilmiş ızgara örneklemesi. Yollara, dereye, göle, köprülere, bölgelere ve
    /// harita kenarına ağaç konmaz. Belirlenimci (tohum).
    /// </summary>
    public static class TreeScatter
    {
        public const int DefaultTargetCount = 3500;

        /// <summary>Örnekleme ızgara aralığı (m) — ağaçlar arası en az ~1.6 m.</summary>
        public const float CandidateSpacing = 4f;

        private const float SnowLine = TerrainPainter.SnowLine;

        private struct Candidate
        {
            public float X, Z, Density;
            public TreeKind Kind;
        }

        /// <summary>Ağaç örneklerini üretir (TerrainData.treeInstances için; konumlar 0..1 normalize).</summary>
        public static TreeInstance[] Scatter(TerrainModel model, Vector3 terrainSize, int seed, int targetCount = DefaultTargetCount)
        {
            if (model == null || targetCount <= 0)
                return new TreeInstance[0];

            var rng = new System.Random(seed * 7349 + 1931);
            var layout = model.Layout;
            var noise = model.Noise;
            var half = layout.HalfSize;
            var cells = Mathf.Max(1, Mathf.FloorToInt(half * 2f / CandidateSpacing));
            var candidates = new List<Candidate>(cells * cells / 6);
            var forest = layout.FindLocation(LocationKind.Forest);
            var ruins = layout.FindLocation(LocationKind.Ruins);
            var total = 0f;

            for (var cz = 0; cz < cells; cz++)
            {
                for (var cx = 0; cx < cells; cx++)
                {
                    var x = -half + (cx + 0.15f + 0.7f * (float)rng.NextDouble()) * CandidateSpacing;
                    var z = -half + (cz + 0.15f + 0.7f * (float)rng.NextDouble()) * CandidateSpacing;
                    var kindRoll = (float)rng.NextDouble();
                    var density = Density(model, noise, forest, ruins, x, z, kindRoll, out var kind);
                    if (density <= 0f)
                        continue;

                    candidates.Add(new Candidate { X = x, Z = z, Density = density, Kind = kind });
                    total += density;
                }
            }

            if (candidates.Count == 0 || total <= 0f)
                return new TreeInstance[0];

            var scale = targetCount / total;
            var result = new List<TreeInstance>(targetCount + 64);
            var size = terrainSize;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                var p = Mathf.Min(1f, c.Density * scale);
                if ((float)rng.NextDouble() >= p)
                    continue;
                if (result.Count >= targetCount * 1.08f)
                    break;

                var h = model.SampleHeight(c.X, c.Z);
                var heightScale = Range(rng, 0.78f, 1.25f);
                var widthScale = heightScale * Range(rng, 0.88f, 1.12f);
                if (c.Kind == TreeKind.Bush)
                {
                    heightScale = Range(rng, 0.7f, 1.4f);
                    widthScale = heightScale * Range(rng, 0.9f, 1.3f);
                }

                var tint = Range(rng, 0.85f, 1f);
                result.Add(new TreeInstance
                {
                    prototypeIndex = (int)c.Kind,
                    position = new Vector3((c.X + half) / size.x, Mathf.Clamp01(h / Mathf.Max(1f, size.y)), (c.Z + half) / size.z),
                    heightScale = heightScale,
                    widthScale = widthScale,
                    rotation = Range(rng, 0f, Mathf.PI * 2f),
                    color = new Color(tint, tint, tint, 1f),
                    lightmapColor = Color.white
                });
            }

            return result.ToArray();
        }

        /// <summary>
        /// Verilen sınır kutularıyla (XZ, margin kadar genişletilmiş) çakışan ağaçları araziden kaldırır. Kaldırılan sayıyı döner.
        /// </summary>
        public static int RemoveTreesInBounds(Terrain terrain, IReadOnlyList<Bounds> bounds, float margin)
        {
            if (terrain == null || terrain.terrainData == null || bounds == null || bounds.Count == 0)
                return 0;

            var data = terrain.terrainData;
            var instances = data.treeInstances;
            if (instances == null || instances.Length == 0)
                return 0;

            var size = data.size;
            var origin = terrain.transform.position;
            var kept = new List<TreeInstance>(instances.Length);
            var removed = 0;
            for (var i = 0; i < instances.Length; i++)
            {
                var t = instances[i];
                var wx = origin.x + t.position.x * size.x;
                var wz = origin.z + t.position.z * size.z;
                var blocked = false;
                for (var b = 0; b < bounds.Count; b++)
                {
                    var box = bounds[b];
                    if (box.size.x <= 0.01f && box.size.z <= 0.01f)
                        continue;
                    if (wx >= box.min.x - margin && wx <= box.max.x + margin && wz >= box.min.z - margin && wz <= box.max.z + margin)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                    removed++;
                else
                    kept.Add(t);
            }

            if (removed > 0)
            {
                data.SetTreeInstances(kept.ToArray(), false);
                WorldAssetPersistence.MarkDirty(data);
            }

            return removed;
        }

        // ================================================================== Yoğunluk

        private static float Density(TerrainModel model, TerrainNoise noise, LocationSpec forest, LocationSpec ruins, float x, float z,
            float kindRoll, out TreeKind kind)
        {
            kind = TreeKind.PineA;
            if (!model.IsClearOfFeatures(x, z, 3.5f, 2.5f, 1f))
                return 0f;

            var edge = model.EdgeDistance(x, z);
            if (edge < 6f)
                return 0f;

            var h = model.SampleHeight(x, z);
            if (h < model.Layout.WaterLevel + 0.8f)
                return 0f;

            var slope = model.SampleSlope(x, z);
            if (slope > 41f)
                return 0f;

            var flatten = model.SampleFlatten(x, z);
            if (flatten > 0.6f)
                return 0f;

            var riverDistance = model.SampleRiverDistance(x, z);
            var patch = noise.Fbm(x / 85f + 41.3f, z / 85f - 17.9f, 3) * 0.5f + 0.5f;     // korular
            var fine = noise.Fbm(x / 23f - 5.1f, z / 23f + 9.4f, 2) * 0.5f + 0.5f;         // açıklıklar
            var slopeFactor = 1f - TerrainNoise.SmoothStep(32f, 41f, slope);

            // ---------------------------------------------------------------- Çam Sırtı ormanı
            if (forest != null)
            {
                var dx = x - forest.Center.x;
                var dz = z - forest.Center.y;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                var forestT = 1f - TerrainNoise.SmoothStep(forest.Radius * 0.75f, forest.Radius * 1.45f, d + (fine - 0.5f) * 30f);
                if (forestT > 0.05f)
                {
                    kind = kindRoll < 0.46f ? TreeKind.PineA : (kindRoll < 0.9f ? TreeKind.PineB : (kindRoll < 0.96f ? TreeKind.Bush : TreeKind.Dead));
                    // Orman içinde birkaç küçük açıklık.
                    var clearing = TerrainNoise.SmoothStep(0.72f, 0.82f, fine);
                    return 1.35f * forestT * slopeFactor * (1f - clearing * 0.85f);
                }
            }

            // ---------------------------------------------------------------- Kar çizgisi üstü: seyrek kuru ağaç
            if (h > SnowLine - 4f)
            {
                kind = TreeKind.Dead;
                return h > SnowLine + 6f ? 0f : 0.015f * slopeFactor;
            }

            // ---------------------------------------------------------------- Harabe çevresi: kuru ağaçlar
            if (ruins != null)
            {
                var dx = x - ruins.Center.x;
                var dz = z - ruins.Center.y;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < ruins.Radius * 2f && kindRoll < 0.5f)
                {
                    kind = kindRoll < 0.25f ? TreeKind.Dead : TreeKind.Bush;
                    return 0.05f * slopeFactor;
                }
            }

            // ---------------------------------------------------------------- Dere kıyısı (çalı + meşe)
            var riverHalf = model.RiverWaterHalfWidth;
            if (riverDistance < riverHalf + 26f)
            {
                var bank = TerrainNoise.SmoothStep(riverHalf + 3f, riverHalf + 7f, riverDistance)
                           * (1f - TerrainNoise.SmoothStep(riverHalf + 16f, riverHalf + 26f, riverDistance));
                kind = kindRoll < 0.55f ? TreeKind.Bush : (kindRoll < 0.93f ? TreeKind.Oak : TreeKind.Dead);
                return 0.22f * bank * slopeFactor * (0.4f + patch);
            }

            // ---------------------------------------------------------------- Sırtlar / dağ yamaçları (çam)
            var ridgeT = TerrainNoise.SmoothStep(52f, 78f, h);
            if (ridgeT > 0.01f)
            {
                kind = kindRoll < 0.5f ? TreeKind.PineA : (kindRoll < 0.9f ? TreeKind.PineB : (kindRoll < 0.97f ? TreeKind.Bush : TreeKind.Dead));
                var clumps = TerrainNoise.SmoothStep(0.42f, 0.66f, patch);
                return 0.3f * ridgeT * clumps * slopeFactor * (0.5f + fine);
            }

            // ---------------------------------------------------------------- Vadi: meşe koruları + çalılar
            var groves = TerrainNoise.SmoothStep(0.52f, 0.7f, patch);
            kind = kindRoll < 0.62f ? TreeKind.Oak : (kindRoll < 0.95f ? TreeKind.Bush : TreeKind.Dead);
            return (0.016f + 0.15f * groves) * slopeFactor * (0.5f + fine);
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
