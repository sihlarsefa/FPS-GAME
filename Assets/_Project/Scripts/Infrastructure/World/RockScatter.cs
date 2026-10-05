using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Kayalık yamaçlara ve sırtlara low-poly kaya serpiştirme. Kayalar 128 m'lik parçalarda tek mesh'te birleştirilir
    /// (parça başına bir MeshRenderer — açık/koyu kaya alt mesh'leri — ve bir MeshCollider), Default katman: siper sağlar,
    /// NavMesh'e engel olarak girer. Yollara, dereye, bölgelere ve yapılara kaya konmaz.
    /// </summary>
    public static class RockScatter
    {
        public const string RootName = "Kayalar";
        public const int DefaultCount = 260;
        public const float ChunkSize = 128f;

        /// <summary>Kayaları üretir; kök nesneyi döner (hiç kaya yoksa null).</summary>
        public static GameObject Scatter(TerrainModel model, Transform parent, int seed, IReadOnlyList<Bounds> structures, int count = DefaultCount)
        {
            if (model == null || count <= 0)
                return null;

            var rng = new System.Random(seed * 9973 + 577);
            var half = model.Layout.HalfSize;
            var chunksPerSide = Mathf.Max(1, Mathf.CeilToInt(half * 2f / ChunkSize));
            var builders = new Dictionary<int, MeshBuilder>();
            var placed = 0;
            var attempts = count * 40;
            var noise = model.Noise;

            for (var a = 0; a < attempts && placed < count; a++)
            {
                var x = Range(rng, -half + 8f, half - 8f);
                var z = Range(rng, -half + 8f, half - 8f);
                var slope = model.SampleSlope(x, z);
                var h = model.SampleHeight(x, z);
                var edge = model.EdgeDistance(x, z);

                // Kayalık: dik yamaçlar, yüksek sırtlar, harita kenarı; düz vadide seyrek.
                var score = TerrainNoise.SmoothStep(18f, 34f, slope) * 0.8f
                            + TerrainNoise.SmoothStep(80f, 130f, h) * 0.35f
                            + (1f - TerrainNoise.SmoothStep(30f, 160f, edge)) * 0.25f
                            + 0.06f;
                score *= 0.55f + 0.9f * (noise.Fbm(x / 60f + 7.7f, z / 60f - 3.3f, 2) * 0.5f + 0.5f);
                if (slope > 55f || (float)rng.NextDouble() > score)
                    continue;
                if (!model.IsClearOfFeatures(x, z, 4f, 4f, 1.05f))
                    continue;
                if (model.SampleFlatten(x, z) > 0.35f)
                    continue;
                if (h < model.Layout.WaterLevel + 0.5f)
                    continue;
                if (OverlapsStructure(structures, x, z, 4f))
                    continue;

                var size = Range(rng, 0.6f, 1.6f);
                if ((float)rng.NextDouble() < 0.18f)
                    size *= Range(rng, 1.6f, 2.6f); // iri kaya bloğu
                var scale = new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.7f, 1.15f), size * Range(rng, 0.9f, 1.3f));
                var normal = model.SampleNormal(x, z);
                var rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.6f))
                               * Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, 0f, 360f), Range(rng, -8f, 8f));
                // Taban mesh'te ~-0.35 → biraz gömülü otursun.
                var position = new Vector3(x, h + 0.05f * scale.y, z);

                var cx = Mathf.Clamp(Mathf.FloorToInt((x + half) / ChunkSize), 0, chunksPerSide - 1);
                var cz = Mathf.Clamp(Mathf.FloorToInt((z + half) / ChunkSize), 0, chunksPerSide - 1);
                var key = cz * chunksPerSide + cx;
                if (!builders.TryGetValue(key, out var builder))
                {
                    builder = new MeshBuilder(2);
                    builders[key] = builder;
                }

                var variant = rng.Next(RockFactory.VariantCount);
                var dark = (float)rng.NextDouble() < 0.35f;
                var origin = ChunkOrigin(cx, cz, half);
                var matrix = Matrix4x4.TRS(position - origin, rotation, scale);
                builder.Append(RockFactory.GetVariant(variant), matrix, dark ? 1 : 0);
                placed++;
            }

            if (builders.Count == 0)
                return null;

            var root = new GameObject(RootName);
            root.layer = GameLayers.Default;
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var materials = new[] { MaterialLibrary.Get(MaterialId.Rock), MaterialLibrary.Get(MaterialId.RockDark) };
            foreach (var pair in builders)
            {
                var cx = pair.Key % chunksPerSide;
                var cz = pair.Key / chunksPerSide;
                var mesh = pair.Value.ToMesh("HK_Rocks_" + cx + "_" + cz);
                var go = new GameObject("Kaya_" + cx + "_" + cz);
                go.layer = GameLayers.Default;
                go.transform.SetParent(root.transform, false);
                go.transform.position = ChunkOrigin(cx, cz, half);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            return root;
        }

        private static Vector3 ChunkOrigin(int cx, int cz, float half)
        {
            return new Vector3(-half + (cx + 0.5f) * ChunkSize, 0f, -half + (cz + 0.5f) * ChunkSize);
        }

        internal static bool OverlapsStructure(IReadOnlyList<Bounds> structures, float x, float z, float margin)
        {
            if (structures == null)
                return false;
            for (var i = 0; i < structures.Count; i++)
            {
                var b = structures[i];
                if (x >= b.min.x - margin && x <= b.max.x + margin && z >= b.min.z - margin && z <= b.max.z + margin)
                    return true;
            }

            return false;
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
