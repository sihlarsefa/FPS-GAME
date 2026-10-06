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
            GameObject overrideRoot = null;

            void PlaceRock(float x, float z, float h, float sizeBoost)
            {
                var size = Range(rng, 0.6f, 1.6f);
                if ((float)rng.NextDouble() < 0.18f)
                    size *= Range(rng, 1.6f, 2.6f); // iri kaya bloğu
                if (sizeBoost > 0f)
                    size = sizeBoost * Range(rng, 0.7f, 1.3f);
                var scale = new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.7f, 1.15f), size * Range(rng, 0.9f, 1.3f));
                var normal = model.SampleNormal(x, z);
                var rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.6f))
                               * Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, 0f, 360f), Range(rng, -8f, 8f));
                // Taban mesh'te ~-0.35 → biraz gömülü otursun.
                var position = new Vector3(x, h + 0.05f * scale.y, z);

                if (TryPlaceOverride(rng, size, position, rotation, scale, ref overrideRoot, parent))
                {
                    placed++;
                    return;
                }

                var cx = Mathf.Clamp(Mathf.FloorToInt((x + half) / ChunkSize), 0, chunksPerSide - 1);
                var cz = Mathf.Clamp(Mathf.FloorToInt((z + half) / ChunkSize), 0, chunksPerSide - 1);
                var key = cz * chunksPerSide + cx;
                if (!builders.TryGetValue(key, out var builder))
                {
                    builder = new MeshBuilder(3);
                    builders[key] = builder;
                }

                var variant = rng.Next(RockFactory.VariantCount);
                var dark = (float)rng.NextDouble() < 0.35f;
                var origin = ChunkOrigin(cx, cz, half);
                var matrix = Matrix4x4.TRS(position - origin, rotation, scale);
                builder.AppendMapped(RockFactory.GetSplitVariant(variant), matrix, new[] { dark ? 1 : 0, 2 });
                placed++;
            }

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

                PlaceRock(x, z, h, 0f);
            }

            // Uçurum kaya kümeleri: dik yüzlerde iri kaya öbekleri (sırt çizgisi karakteri).
            var clusterSeeds = ClusterSeedCount(count);
            var clusterRng = new System.Random(seed * 7919 + 131);
            for (var c = 0; c < clusterSeeds * 60 && clusterSeeds > 0; c++)
            {
                var cx0 = Range(clusterRng, -half + 12f, half - 12f);
                var cz0 = Range(clusterRng, -half + 12f, half - 12f);
                var cs = model.SampleSlope(cx0, cz0);
                var ch = model.SampleHeight(cx0, cz0);
                if (!OutcropAllowed(cs, ch, model.SampleFlatten(cx0, cz0)))
                    continue;
                if (!model.IsClearOfFeatures(cx0, cz0, 6f, 6f, 1.05f) || OverlapsStructure(structures, cx0, cz0, 6f))
                    continue;
                clusterSeeds--;
                var members = 5 + clusterRng.Next(5);
                for (var m = 0; m < members; m++)
                {
                    var ang = Range(clusterRng, 0f, 6.2832f);
                    var rad = Range(clusterRng, 0.5f, 9f);
                    var mx = cx0 + Mathf.Cos(ang) * rad;
                    var mz = cz0 + Mathf.Sin(ang) * rad;
                    if (Mathf.Abs(mx) > half - 8f || Mathf.Abs(mz) > half - 8f)
                        continue;
                    var mh = model.SampleHeight(mx, mz);
                    if (mh < model.Layout.WaterLevel + 0.5f || model.SampleFlatten(mx, mz) > 0.35f
                        || !model.IsClearOfFeatures(mx, mz, 4f, 4f, 1.05f) || OverlapsStructure(structures, mx, mz, 4f))
                        continue;
                    PlaceRock(mx, mz, mh, OutcropRockSize(m, Range(clusterRng, 0f, 1f)));
                }
            }

            if (builders.Count == 0 && overrideRoot == null)
                return null;

            var root = overrideRoot != null ? overrideRoot : new GameObject(RootName);
            root.layer = GameLayers.Default;
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var materials = new[] { MaterialLibrary.Get(MaterialId.Rock), MaterialLibrary.Get(MaterialId.RockDark), VegetationMaterials.CreateMoss() };
            foreach (var pair in builders)
            {
                if (pair.Value == null)
                    continue;
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

        /// <summary>
        /// RockOverride varsa kayayı prefab örneği olarak yerleştirir (aynı prefab/materyal → GPU Resident Drawer uyumlu).
        /// Collider yoksa renderer sınırlarından BoxCollider eklenir (siper + NavMesh engeli).
        /// </summary>
        private static bool TryPlaceOverride(System.Random rng, float size, Vector3 position, Quaternion rotation, Vector3 scale,
            ref GameObject root, Transform parent)
        {
            GameObject prefab;
            try
            {
                if (!Project.Infrastructure.Content.ContentOverrides.TryGetRock(VegetationTuning.RockSizeClass(size), out prefab) || prefab == null)
                    return false;
            }
            catch (System.Exception)
            {
                return false;
            }

            if (root == null)
            {
                root = new GameObject(RootName);
                root.layer = GameLayers.Default;
                if (parent != null)
                    root.transform.SetParent(parent, false);
            }

            var go = Object.Instantiate(prefab, root.transform);
            go.name = "Kaya_" + prefab.name;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(scale.x, scale.y, scale.z) / Mathf.Max(0.01f, size));
            if (go.GetComponentInChildren<Collider>(true) == null)
            {
                var renderer = go.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    var box = go.AddComponent<BoxCollider>();
                    var b = renderer.bounds;
                    box.center = go.transform.InverseTransformPoint(b.center);
                    var ls = go.transform.lossyScale;
                    box.size = new Vector3(b.size.x / Mathf.Max(0.001f, ls.x), b.size.y / Mathf.Max(0.001f, ls.y), b.size.z / Mathf.Max(0.001f, ls.z));
                }
            }

            return true;
        }

        /// <summary>Kaya kümesi tohum sayısı (toplam kaya sayısının ~%10'u, en az 1).</summary>
        public static int ClusterSeedCount(int count)
        {
            return count <= 0 ? 0 : Mathf.Max(1, count / 10);
        }

        /// <summary>Küme tohumu uygunluğu: dik yüz (30-62°), yeterince yüksek, düzleştirilmiş alan dışı.</summary>
        public static bool OutcropAllowed(float slopeDegrees, float height, float flatten)
        {
            return slopeDegrees >= 30f && slopeDegrees <= 62f && height > 25f && flatten <= 0.2f;
        }

        /// <summary>Küme üyesi kaya boyutu (m): ilk üye anıt blok, diğerleri 2.2-4.6.</summary>
        public static float OutcropRockSize(int memberIndex, float t01)
        {
            return memberIndex == 0 ? 4.5f + 2f * t01 : 2.2f + 2.4f * t01;
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
