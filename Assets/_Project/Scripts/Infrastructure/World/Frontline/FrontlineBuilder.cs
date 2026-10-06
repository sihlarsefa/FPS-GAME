using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// <see cref="FrontlinePlan"/> planını sahneye kurar (prosedürel mesh). Kum torbası/toprak/tel/kirpi/kütük birleşik
    /// mesh'lerde (statik batching ve MaterialPropertyBlock yok → GPU Resident Drawer uyumlu); krater: araziye oturan
    /// koyu zemin diski + kenar toprak halkası. Yalnız Kuzgun haritasında, kalite kademesine göre yoğunlukla.
    /// </summary>
    public static class FrontlineBuilder
    {
        public const string RootName = "CepheHatti";
        private const string FromPoi = "Yamaç Köyü";
        private const string ToPoi = "İleri Üs Bölgesi";
        private const int Sandbag = 0, Dirt = 1, Metal = 2, Wood = 3, Mud = 4;
        private const int MaxVerts = 60000;

        /// <summary>Kuzgun haritası değilse / POI yoksa hiçbir şey yapmaz; kök nesneyi döner.</summary>
        public static GameObject BuildIfApplicable(MapLayout layout, TerrainModel model, Transform parent, int seed, IList<Bounds> structures, bool headless)
        {
            if (layout == null || model == null || headless || MapCatalog.Normalize(layout.Name) != MapCatalog.Kuzgun)
                return null;
            var from = layout.FindLocation(FromPoi);
            var to = layout.FindLocation(ToPoi);
            if (from == null || to == null)
                return null;
            var dir = (to.Center - from.Center).normalized;
            var a = from.Center + dir * (from.Radius + 10f);
            var b = to.Center - dir * (to.Radius + 10f);
            var tier = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);
            var plan = FrontlinePlan.Create(a, b, seed, tier, (x, z) => IsFree(model, structures, x, z));
            return Build(plan, model, parent);
        }

        private static bool IsFree(TerrainModel model, IList<Bounds> structures, float x, float z)
        {
            if (!model.IsClearOfFeatures(x, z, 4f, 5f, 1f) || model.SampleSlope(x, z) > 28f)
                return false;
            if (structures != null)
                for (var i = 0; i < structures.Count; i++)
                    if (structures[i].Contains(new Vector3(x, structures[i].center.y, z)))
                        return false;
            return true;
        }

        public static GameObject Build(FrontlinePlan plan, TerrainModel model, Transform parent)
        {
            if (plan == null)
                return null;
            var root = new GameObject(RootName);
            root.layer = GameLayers.Default;
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var mats = new[]
            {
                MaterialLibrary.Get(MaterialId.Sandbag), MaterialLibrary.Get(MaterialId.Dirt), MaterialLibrary.Get(MaterialId.Rust),
                MaterialLibrary.Get(MaterialId.DeadWood), MaterialLibrary.Get(MaterialId.Mud)
            };
            var chunk = 0;
            var b = new MeshBuilder(5);
            void Flush()
            {
                if (b.VertexCount == 0)
                    return;
                var mesh = b.ToMesh("HK_Cephe_" + chunk);
                var go = new GameObject("CepheHatti_" + chunk++);
                go.layer = GameLayers.Default;
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                b = new MeshBuilder(5);
            }

            Vector3 P(Vector2 p, float lift = 0f) => new Vector3(p.x, model.SampleHeight(p.x, p.y) + lift, p.y);

            // Siper: iki yanda toprak sedde (parapet) şeritleri + koyu zemin şeridi.
            for (var i = 0; i + 1 < plan.Trench.Count; i++)
            {
                var p0 = plan.Trench[i];
                var p1 = plan.Trench[i + 1];
                var sd = (p1 - p0).normalized;
                var sn = new Vector2(-sd.y, sd.x);
                var mid = (p0 + p1) * 0.5f;
                var len = (p1 - p0).magnitude;
                var rot = Quaternion.LookRotation(new Vector3(sd.x, 0f, sd.y));
                MeshFactory.AddBox(b, Mud, P(mid, 0.02f), new Vector3(FrontlinePlan.TrenchWidth, 0.04f, len), rot);
                for (var s = -1; s <= 1; s += 2)
                {
                    var c = mid + sn * (s * (FrontlinePlan.TrenchWidth * 0.5f + 0.45f));
                    MeshFactory.AddBox(b, Dirt, P(c, 0.25f), new Vector3(0.9f, 0.5f, len), rot);
                }

                if (b.VertexCount > MaxVerts)
                    Flush();
            }

            foreach (var s in plan.Sandbags)
            {
                var rot = Quaternion.Euler(0f, s.Yaw, 0f);
                var along = rot * Vector3.forward;
                var rows = (int)s.Size2;
                var count = Mathf.Max(2, Mathf.RoundToInt(s.Size / 0.55f));
                for (var r = 0; r < rows; r++)
                    for (var k = 0; k < count; k++)
                    {
                        var u = (k - (count - 1) * 0.5f) * 0.55f + (r % 2) * 0.27f;
                        var pos = P(s.Position) + along * u + Vector3.up * (0.12f + r * 0.2f);
                        MeshFactory.AddBox(b, Sandbag, pos, new Vector3(0.45f, 0.2f, 0.28f), rot);
                    }

                if (b.VertexCount > MaxVerts)
                    Flush();
            }

            foreach (var f in plan.Foxholes)
            {
                AddDisc(b, model, f.Position, f.Size, Mud, 0.03f, 10);
                AddRing(b, model, f.Position, f.Size, f.Size + 0.5f, Dirt, 10, 0.3f);
                if (b.VertexCount > MaxVerts)
                    Flush();
            }

            foreach (var c in plan.Craters)
            {
                AddDisc(b, model, c.Position, c.Size, Mud, 0.04f, 14);
                AddRing(b, model, c.Position, c.Size, c.Size + 0.7f, Dirt, 14, 0.35f);
                if (b.VertexCount > MaxVerts)
                    Flush();
            }

            foreach (var line in plan.WireLines)
            {
                for (var i = 0; i < line.Count; i++)
                {
                    MeshFactory.AddBox(b, Metal, P(line[i], 0.6f), new Vector3(0.08f, 1.2f, 0.08f));
                    if (i + 1 >= line.Count)
                        continue;
                    var q = line[i + 1] - line[i];
                    var rot = Quaternion.LookRotation(new Vector3(q.x, 0f, q.y));
                    var mid = (line[i] + line[i + 1]) * 0.5f;
                    for (var h = 0; h < 3; h++)
                        MeshFactory.AddBox(b, Metal, P(mid, 0.3f + h * 0.35f), new Vector3(0.025f, 0.025f, q.magnitude), rot);
                }

                if (b.VertexCount > MaxVerts)
                    Flush();
            }

            foreach (var h in plan.Hedgehogs)
            {
                var pos = P(h.Position, h.Size * 0.5f);
                for (var k = 0; k < 3; k++)
                {
                    var rot = Quaternion.Euler(k == 0 ? 45f : k == 1 ? -45f : 0f, h.Yaw + k * 60f, k == 2 ? 90f : 0f);
                    MeshFactory.AddBox(b, Metal, pos, new Vector3(0.12f, 0.12f, h.Size * 1.5f), rot);
                }
            }

            foreach (var l in plan.Logs)
            {
                var rot = Quaternion.Euler(0f, l.Yaw, 0f);
                MeshFactory.AddBox(b, Wood, P(l.Position, 0.25f), new Vector3(0.45f, 0.45f, l.Size), rot);
                if (b.VertexCount > MaxVerts)
                    Flush();
            }

            Flush();
            Debug.Log("[Cephe] Siper " + plan.Trench.Count + ", torba " + plan.Sandbags.Count + ", krater " + plan.Craters.Count + ".");
            return root;
        }

        private static void AddDisc(MeshBuilder b, TerrainModel m, Vector2 c, float r, int sub, float lift, int seg)
        {
            var center = new Vector3(c.x, m.SampleHeight(c.x, c.y) + lift, c.y);
            for (var i = 0; i < seg; i++)
            {
                var a0 = i * Mathf.PI * 2f / seg;
                var a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = RimPoint(m, c, r, a0, lift);
                var p1 = RimPoint(m, c, r, a1, lift);
                b.AddFlatTriangle(sub, center, p1, p0);
            }
        }

        private static void AddRing(MeshBuilder b, TerrainModel m, Vector2 c, float r0, float r1, int sub, int seg, float height)
        {
            for (var i = 0; i < seg; i++)
            {
                var a0 = i * Mathf.PI * 2f / seg;
                var a1 = (i + 1) * Mathf.PI * 2f / seg;
                var i0 = RimPoint(m, c, r0, a0, 0.03f);
                var i1 = RimPoint(m, c, r0, a1, 0.03f);
                var o0 = RimPoint(m, c, r1, a0, 0.03f) + Vector3.up * height * 0.5f;
                var o1 = RimPoint(m, c, r1, a1, 0.03f) + Vector3.up * height * 0.5f;
                var t0 = RimPoint(m, c, (r0 + r1) * 0.5f, a0, height);
                var t1 = RimPoint(m, c, (r0 + r1) * 0.5f, a1, height);
                // İç yamaç + dış yamaç (iki yüz: yönden bağımsız görünür).
                b.AddFlatQuad(sub, i0, t0, t1, i1);
                b.AddFlatQuad(sub, i1, t1, t0, i0);
                b.AddFlatQuad(sub, t0, o0, o1, t1);
                b.AddFlatQuad(sub, t1, o1, o0, t0);
            }
        }

        private static Vector3 RimPoint(TerrainModel m, Vector2 c, float r, float ang, float lift)
        {
            var x = c.x + Mathf.Cos(ang) * r;
            var z = c.y + Mathf.Sin(ang) * r;
            return new Vector3(x, m.SampleHeight(x, z) + lift, z);
        }
    }
}
