using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Tarla çevresi dünya propları: alçak taş duvarlar (parsel başına tek birleşik mesh + MeshCollider → NavMesh engeli,
    /// kapı boşlukları bırakılır), kaya yığınları (aynı mesh'te), saman balyaları (PropFactory) ve çalı çitleri
    /// (arazi ağacı örnekleri, <see cref="TreeKind.Bush"/>). Seed'e bağlı deterministik; yollar/dere/bölgelere girmez.
    /// </summary>
    public static class FieldDetailProps
    {
        public const string RootName = "SahaDetay";
        public const int MaxHayBales = 48;
        private const float BlockLength = 1.5f;
        private const float BushSpacing = 3.4f;

        /// <summary>Duvar, kaya yığını ve balyaları üretir; kök nesneyi döner (parsel yoksa null).</summary>
        public static GameObject Build(TerrainModel model, Transform parent, int seed)
        {
            var plan = FieldPlan.Of(model);
            if (plan == null || plan.Parcels.Count == 0)
                return null;

            var root = new GameObject(RootName);
            root.layer = GameLayers.Default;
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var rng = new System.Random(seed * 4093 + 811);
            var materials = new[] { MaterialLibrary.Get(MaterialId.Rock), MaterialLibrary.Get(MaterialId.RockDark), VegetationMaterials.CreateMoss() };
            var hay = 0;
            for (var i = 0; i < plan.Parcels.Count; i++)
            {
                var p = plan.Parcels[i];
                var origin = new Vector3(p.Center.x, model.SampleHeight(p.Center.x, p.Center.y), p.Center.y);
                var b = new MeshBuilder(3);
                var any = false;
                for (var side = 0; side < 4; side++)
                {
                    if ((p.WallMask & (1 << side)) != 0)
                        any |= AddWall(model, p, side, origin, b, rng);
                }

                if (rng.NextDouble() < 0.45)
                    any |= AddRockPile(model, p, origin, b, rng);

                if (any)
                {
                    var mesh = b.ToMesh("HK_FieldWalls_" + i);
                    var go = new GameObject("TarlaDuvari_" + i);
                    go.layer = GameLayers.Default;
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = origin;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }

                if (hay < MaxHayBales && p.Kind != FieldKind.Fallow)
                    hay += AddHayBales(model, p, root.transform, rng, MaxHayBales - hay);
            }

            return root;
        }

        /// <summary>Çalı çiti ağaç örnekleri (Bush prototipi). size: TerrainData.size.</summary>
        public static List<TreeInstance> HedgeTrees(TerrainModel model, Vector3 size, int seed)
        {
            var result = new List<TreeInstance>();
            var plan = FieldPlan.Of(model);
            if (plan == null)
                return result;
            var half = model.Layout.HalfSize;
            var rng = new System.Random(seed * 1597 + 331);
            for (var i = 0; i < plan.Parcels.Count; i++)
            {
                var p = plan.Parcels[i];
                for (var side = 0; side < 4; side++)
                {
                    if ((p.HedgeMask & (1 << side)) == 0)
                        continue;
                    SideEnds(p, side, out var a, out var d, out var length);
                    var gate = GatePosition(rng, length);
                    for (var t = BushSpacing * 0.5f; t < length; t += BushSpacing * (0.8f + (float)rng.NextDouble() * 0.5f))
                    {
                        if (Mathf.Abs(t - gate) < 2.6f)
                            continue;
                        var w = p.ToWorld(a.x + d.x * t, a.y + d.y * t);
                        if (!model.IsClearOfFeatures(w.x, w.y, 3f, 4f, 1.0f))
                            continue;
                        var h = model.SampleHeight(w.x, w.y);
                        var tone = 0.82f + (float)rng.NextDouble() * 0.18f;
                        result.Add(new TreeInstance
                        {
                            prototypeIndex = (int)TreeKind.Bush,
                            position = new Vector3((w.x + half) / size.x, Mathf.Clamp01(h / Mathf.Max(1f, size.y)), (w.y + half) / size.z),
                            heightScale = 0.95f + (float)rng.NextDouble() * 0.5f,
                            widthScale = 1.0f + (float)rng.NextDouble() * 0.5f,
                            rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                            color = new Color(tone, tone, tone * 0.95f, 1f),
                            lightmapColor = Color.white
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>Parsel içine düşen arazi ağaçlarını eler (tarlada ağaç yok; çalı çitleri sonradan eklenir).</summary>
        public static TreeInstance[] FilterTreesInFields(TreeInstance[] trees, TerrainModel model, Vector3 size)
        {
            var plan = FieldPlan.Of(model);
            if (trees == null || plan == null || plan.Parcels.Count == 0)
                return trees;
            var half = model.Layout.HalfSize;
            var kept = new List<TreeInstance>(trees.Length);
            for (var i = 0; i < trees.Length; i++)
            {
                var t = trees[i];
                if (!plan.InsideAny(t.position.x * size.x - half, t.position.z * size.z - half, 1.5f))
                    kept.Add(t);
            }

            return kept.ToArray();
        }

        // ------------------------------------------------------------------ Yardımcılar

        /// <summary>Kenar başlangıcı (yerel), birim yön (yerel) ve uzunluk.</summary>
        internal static void SideEnds(FieldParcel p, int side, out Vector2 start, out Vector2 dir, out float length)
        {
            switch (side)
            {
                case 0: start = new Vector2(p.HalfX, -p.HalfZ); dir = Vector2.up; length = p.HalfZ * 2f; break;
                case 1: start = new Vector2(-p.HalfX, -p.HalfZ); dir = Vector2.up; length = p.HalfZ * 2f; break;
                case 2: start = new Vector2(-p.HalfX, p.HalfZ); dir = Vector2.right; length = p.HalfX * 2f; break;
                default: start = new Vector2(-p.HalfX, -p.HalfZ); dir = Vector2.right; length = p.HalfX * 2f; break;
            }
        }

        internal static float GatePosition(System.Random rng, float length)
        {
            return length * (0.25f + (float)rng.NextDouble() * 0.5f);
        }

        private static bool AddWall(TerrainModel model, FieldParcel p, int side, Vector3 origin, MeshBuilder b, System.Random rng)
        {
            SideEnds(p, side, out var a, out var d, out var length);
            var gate = GatePosition(rng, length);
            var wallYaw = Mathf.Atan2(p.Sin * d.x + p.Cos * d.y, p.Cos * d.x - p.Sin * d.y) * Mathf.Rad2Deg; // yerel yönün dünya açısı (atan2(z,x))
            var added = false;
            var index = 0;
            for (var t = BlockLength * 0.5f; t < length; t += BlockLength, index++)
            {
                if (Mathf.Abs(t - gate) < 2.0f)
                    continue;
                var w = p.ToWorld(a.x + d.x * t, a.y + d.y * t);
                if (!model.IsClearOfFeatures(w.x, w.y, 3f, 5f, 1.0f))
                    continue;
                var ground = model.SampleHeight(w.x, w.y);
                var h = 0.55f + (float)rng.NextDouble() * 0.3f;
                var thick = 0.5f + (float)rng.NextDouble() * 0.15f;
                var center = new Vector3(w.x, ground + h * 0.5f - 0.12f, w.y) - origin;
                var rot = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 4f, -wallYaw + (float)(rng.NextDouble() - 0.5) * 6f, (float)(rng.NextDouble() - 0.5) * 4f);
                MeshFactory.AddBox(b, rng.NextDouble() < 0.3 ? 1 : 0, center, new Vector3(BlockLength * 1.02f, h + 0.24f, thick), rot);
                added = true;

                // Üst taşlar: duvar silüetini kırar.
                if (index % 4 == 1)
                {
                    var s = 0.32f + (float)rng.NextDouble() * 0.18f;
                    var m = Matrix4x4.TRS(new Vector3(w.x, ground + h + 0.02f, w.y) - origin,
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(s * 1.5f, s, s * 1.2f));
                    b.AppendMapped(RockFactory.GetSplitVariant(rng.Next(RockFactory.VariantCount)), m, new[] { 0, 2 });
                }
            }

            return added;
        }

        private static bool AddRockPile(TerrainModel model, FieldParcel p, Vector3 origin, MeshBuilder b, System.Random rng)
        {
            var corner = p.ToWorld(((rng.Next(2) * 2) - 1) * (p.HalfX + 2.2f), ((rng.Next(2) * 2) - 1) * (p.HalfZ + 2.2f));
            if (!model.IsClearOfFeatures(corner.x, corner.y, 4f, 6f, 1.05f))
                return false;
            var count = 4 + rng.Next(3);
            for (var i = 0; i < count; i++)
            {
                var x = corner.x + ((float)rng.NextDouble() - 0.5f) * 2.6f;
                var z = corner.y + ((float)rng.NextDouble() - 0.5f) * 2.6f;
                var s = 0.45f + (float)rng.NextDouble() * 0.7f;
                var pos = new Vector3(x, model.SampleHeight(x, z) + 0.04f * s, z) - origin;
                var m = Matrix4x4.TRS(pos, Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 16f, (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() - 0.5) * 16f),
                    new Vector3(s * 1.2f, s * 0.85f, s));
                b.AppendMapped(RockFactory.GetSplitVariant(rng.Next(RockFactory.VariantCount)), m, new[] { rng.NextDouble() < 0.3 ? 1 : 0, 2 });
            }

            return true;
        }

        private static int AddHayBales(TerrainModel model, FieldParcel p, Transform parent, System.Random rng, int budget)
        {
            if (rng.NextDouble() > 0.55)
                return 0;
            var count = Mathf.Min(budget, 2 + rng.Next(3));
            var placed = 0;
            for (var i = 0; i < count; i++)
            {
                var lx = ((float)rng.NextDouble() * 2f - 1f) * (p.HalfX - 3f);
                var lz = ((float)rng.NextDouble() * 2f - 1f) * (p.HalfZ - 3f);
                var w = p.ToWorld(lx, lz);
                if (!model.IsClearOfFeatures(w.x, w.y, 4f, 6f, 1.05f))
                    continue;
                var pos = new Vector3(w.x, model.SampleHeight(w.x, w.y), w.y);
                PropFactory.HayBale(parent, pos, (float)rng.NextDouble() * 360f, rng);
                placed++;
            }

            return placed;
        }
    }
}
