using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Yol kenarı detayları: ana yolda ahşap elektrik direkleri + katener tel, km taşları, Türkçe tabelalar (bitmap atlas), yanmış araç kasaları, eğimi izleyen korkuluk.
    /// Yol ekseni TerrainModel.RoadProfiles'tan (pürüzsüz Catmull-Rom) okunur. Tek birleşik mesh + az sayıda kutu çarpıştırıcı
    /// (prop bütçesi RoadsidePlan.*For(kademe)). Deterministik (RNG yok); köprü, su, kavşak ve yapılardan kaçınır.
    /// </summary>
    public static class RoadsideDetails
    {
        public const string RootName = "YolDetayları";
        private const int SubWood = 0, SubConcrete = 1, SubMetal = 2, SubWire = 3, SubRed = 4, SubRust = 5, SubWireChar = SubWire;
        private const float PoleHeight = 7.2f;
        private const int WireSegments = 8;

        public static int Build(MapLayout layout, TerrainModel model, Terrain terrain, Transform parent,
            IReadOnlyList<Bounds> structures)
        {
            if (layout == null || model == null || layout.Roads == null || layout.Roads.Count == 0)
                return 0;

            var yOffset = terrain != null ? terrain.transform.position.y : 0f;
            var builder = new MeshBuilder(6);
            var signBuilder = new MeshBuilder(1);
            var root = new GameObject(RootName) { layer = GameLayers.Default };
            if (parent != null)
                root.transform.SetParent(parent, false);

            var tier = QualitySettings.GetQualityLevel();
            var poles = 0;
            var stones = 0;
            var runs = 0;
            var signs = 0;
            var wrecks = 0;
            var guardIndex = 0;
            for (var r = 0; r < layout.Roads.Count; r++)
            {
                var road = layout.Roads[r];
                if (road == null || r >= model.RoadProfiles.Count)
                    continue;
                var profile = model.RoadProfiles[r];
                if (profile == null || profile.Length < 2)
                    continue;

                var samples = RoadsidePlan.Resample(profile, 4f);
                if (samples.Count < 2)
                    continue;

                var width = RoadsidePlan.WidthFor(road.Kind, road.Width);
                var rr = r;
                System.Func<float, float, bool> blocked = (x, z) =>
                    IsBlocked(layout, model, structures, rr, x, z);

                if (RoadsidePlan.IsMainRoad(road.Kind))
                {
                    var side = (r & 1) == 0 ? 1f : -1f;
                    var plan = RoadsidePlan.PlanPoles(samples, width, side,
                        Mathf.Max(0, RoadsidePlan.PolesFor(tier) - poles), blocked);
                    BuildPoles(builder, root, plan, terrain, yOffset, model);
                    poles += plan.Count;

                    var km = RoadsidePlan.PlanKmStones(samples, width,
                        Mathf.Max(0, RoadsidePlan.KmStonesFor(tier) - stones), blocked);
                    for (var i = 0; i < km.Count; i++)
                        AddKmStone(builder, root, km[i], terrain, yOffset);
                    stones += km.Count;

                    var signPlan = RoadsidePlan.PlanSigns(samples, width, RoadsideSignSet.Count, r,
                        Mathf.Max(0, RoadsidePlan.SignsFor(tier) - signs), blocked);
                    for (var i = 0; i < signPlan.Count; i++)
                        AddSign(builder, signBuilder, root, signPlan[i], terrain, yOffset, model);
                    signs += signPlan.Count;

                    var wreckPlan = RoadsidePlan.PlanWrecks(samples, width,
                        Mathf.Max(0, RoadsidePlan.WrecksFor(tier) - wrecks), blocked);
                    for (var i = 0; i < wreckPlan.Count; i++)
                        AddWreck(builder, root, wreckPlan[i], terrain, yOffset, model);
                    wrecks += wreckPlan.Count;
                }

                var rails = RoadsidePlan.PlanGuardrails(samples, width,
                    Mathf.Max(0, RoadsidePlan.GuardRunsFor(tier) - runs), model.SampleHeight, blocked);
                for (var i = 0; i < rails.Count; i++)
                    AddGuardrail(builder, root, rails[i], guardIndex++);
                runs += rails.Count;
            }

            if (builder.VertexCount == 0)
            {
                Object.Destroy(root);
                return 0;
            }

            var mesh = builder.ToMesh("HK_Roadside");
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                MaterialLibrary.Get(MaterialId.WoodDark),
                MaterialLibrary.Get(MaterialId.Concrete),
                MaterialLibrary.Get(MaterialId.MetalPanel),
                MaterialLibrary.Get(MaterialId.Black),
                MaterialLibrary.Get(MaterialId.Red),
                MaterialLibrary.Get(MaterialId.Rust)
            };

            if (signBuilder.VertexCount > 0)
            {
                var signGo = new GameObject("Tabelalar") { layer = GameLayers.Default };
                signGo.transform.SetParent(root.transform, false);
                signGo.AddComponent<MeshFilter>().sharedMesh = signBuilder.ToMesh("HK_RoadsideSigns");
                var signRenderer = signGo.AddComponent<MeshRenderer>();
                signRenderer.sharedMaterial = MaterialLibrary.Textured(RoadsideSignSet.Atlas(), Color.white, 0.12f);
            }

            StructureKit.MarkStatic(root);
            return poles + stones + runs + signs + wrecks;
        }

        private static float Ground(TerrainModel model, Terrain terrain, float yOffset, float x, float z)
        {
            if (terrain != null && terrain.terrainData != null)
                return terrain.SampleHeight(new Vector3(x, 0f, z)) + yOffset;
            return model.SampleHeight(x, z);
        }

        private static void BuildPoles(MeshBuilder b, GameObject root, List<RoadsidePost> poles, Terrain terrain,
            float yOffset, TerrainModel model)
        {
            var tops = new List<(Vector3 left, Vector3 right, Vector3 center)>(poles.Count);
            for (var i = 0; i < poles.Count; i++)
            {
                var pole = poles[i];
                var y = Ground(model, terrain, yOffset, pole.Pos.x, pole.Pos.z);
                var basePos = new Vector3(pole.Pos.x, y, pole.Pos.z);
                var rot = Quaternion.Euler(0f, pole.Yaw, 0f);
                MeshFactory.AddBox(b, SubWood, basePos + Vector3.up * (PoleHeight * 0.5f - 0.3f),
                    new Vector3(0.24f, PoleHeight, 0.24f), rot);
                var armY = basePos + Vector3.up * (PoleHeight - 0.55f);
                MeshFactory.AddBox(b, SubWood, armY, new Vector3(1.9f, 0.12f, 0.12f), rot);
                var right = rot * Vector3.right;
                tops.Add((armY - right * 0.85f + Vector3.up * 0.1f, armY + right * 0.85f + Vector3.up * 0.1f, armY));

                var col = root.AddComponent<BoxCollider>();
                col.center = basePos + Vector3.up * (PoleHeight * 0.5f - 0.3f);
                col.size = new Vector3(0.3f, PoleHeight, 0.3f);
            }

            for (var i = 0; i + 1 < tops.Count; i++)
            {
                var span = Vector3.Distance(tops[i].center, tops[i + 1].center);
                if (span > RoadsidePlan.PoleSpacing * 1.7f || span < 2f)
                    continue; // aradaki direk atlandıysa (köprü/kavşak) tel çekme
                if (b.VertexCount > 120000)
                    return;
                var sag = RoadsidePlan.SagFor(span);
                AddWire(b, tops[i].left, tops[i + 1].left, sag);
                AddWire(b, tops[i].right, tops[i + 1].right, sag);
            }
        }

        private static void AddWire(MeshBuilder b, Vector3 a, Vector3 c, float sag)
        {
            var prev = a;
            for (var s = 1; s <= WireSegments; s++)
            {
                var t = s / (float)WireSegments;
                var p = Vector3.Lerp(a, c, t) + Vector3.down * RoadsidePlan.CatenaryDrop(t, Vector3.Distance(a, c), sag);
                var d = p - prev;
                var len = d.magnitude;
                if (len > 0.01f)
                    MeshFactory.AddBox(b, SubWire, (p + prev) * 0.5f, new Vector3(0.025f, 0.025f, len),
                        Quaternion.LookRotation(d / len));
                prev = p;
            }
        }

        private static void AddKmStone(MeshBuilder b, GameObject root, RoadsidePost post, Terrain terrain, float yOffset)
        {
            var y = terrain != null && terrain.terrainData != null
                ? terrain.SampleHeight(new Vector3(post.Pos.x, 0f, post.Pos.z)) + yOffset
                : post.Pos.y;
            var rot = Quaternion.Euler(0f, post.Yaw, 0f);
            var basePos = new Vector3(post.Pos.x, y, post.Pos.z);
            MeshFactory.AddBox(b, SubConcrete, basePos + Vector3.up * 0.4f, new Vector3(0.35f, 0.9f, 0.18f), rot);
            MeshFactory.AddBox(b, SubRed, basePos + Vector3.up * 0.72f, new Vector3(0.37f, 0.22f, 0.2f), rot);
            var col = root.AddComponent<BoxCollider>();
            col.center = basePos + Vector3.up * 0.4f;
            col.size = new Vector3(0.5f, 0.9f, 0.5f);
        }

        private static void AddSign(MeshBuilder b, MeshBuilder signs, GameObject root, RoadsidePost post, Terrain terrain,
            float yOffset, TerrainModel model)
        {
            var y = Ground(model, terrain, yOffset, post.Pos.x, post.Pos.z);
            RoadsideProps.AddSign(b, SubMetal, signs, new Vector3(post.Pos.x, y, post.Pos.z), post.Yaw, post.Kind);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(post.Pos.x, y + 1.2f, post.Pos.z);
            col.size = new Vector3(0.3f, 2.4f, 0.3f);
        }

        private static void AddWreck(MeshBuilder b, GameObject root, RoadsidePost post, Terrain terrain, float yOffset,
            TerrainModel model)
        {
            var y = Ground(model, terrain, yOffset, post.Pos.x, post.Pos.z);
            var basePos = new Vector3(post.Pos.x, y, post.Pos.z);
            RoadsideProps.AddWreck(b, SubRust, SubWireChar, basePos, post.Yaw, post.Kind);
            var go = new GameObject("YanmisArac" + post.Kind) { layer = GameLayers.Default };
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(basePos + Vector3.up * 0.9f, Quaternion.Euler(0f, post.Yaw, 0f));
            go.AddComponent<BoxCollider>().size = new Vector3(2f, 1.5f, 4.4f);
        }

        /// <summary>Korkuluk: eğimi izler. Her segment kendi eğim açısıyla döner; direkler yol noktalarında; çarpıştırıcı ~16 m'lik gruplar.</summary>
        private static void AddGuardrail(MeshBuilder b, GameObject root, GuardRun run, int index)
        {
            var path = RoadsidePlan.RunPath(run, 4f);
            if (path.Count < 2 || Vector3.Distance(run.Start, run.End) < 0.5f)
                return;

            for (var i = 0; i + 1 < path.Count; i++)
            {
                var d = path[i + 1] - path[i];
                var len = d.magnitude;
                if (len < 0.05f)
                    continue;
                var rot = Quaternion.LookRotation(d / len);
                MeshFactory.AddBox(b, SubMetal, (path[i] + path[i + 1]) * 0.5f + Vector3.up * 0.7f,
                    new Vector3(0.08f, 0.3f, len + 0.04f), rot);
                MeshFactory.AddBox(b, SubWood, path[i] + Vector3.up * 0.45f, new Vector3(0.12f, 0.9f, 0.12f), rot);
            }

            var lastDir = path[path.Count - 1] - path[path.Count - 2];
            if (lastDir.sqrMagnitude > 1e-6f)
                MeshFactory.AddBox(b, SubWood, path[path.Count - 1] + Vector3.up * 0.45f, new Vector3(0.12f, 0.9f, 0.12f),
                    Quaternion.LookRotation(lastDir));

            const int chunk = 4;
            for (var i = 0; i + 1 < path.Count; i += chunk)
            {
                var a = path[i];
                var c = path[Mathf.Min(i + chunk, path.Count - 1)];
                var d = c - a;
                var len = d.magnitude;
                if (len < 0.5f)
                    continue;
                var go = new GameObject("Korkuluk" + index + "_" + i) { layer = GameLayers.Default };
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation((a + c) * 0.5f + Vector3.up * 0.6f, Quaternion.LookRotation(d / len));
                go.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1.1f, len);
            }
        }

        private static bool IsBlocked(MapLayout layout, TerrainModel model, IReadOnlyList<Bounds> structures,
            int roadIndex, float x, float z)
        {
            if (model.SampleHeight(x, z) < layout.WaterLevel + 0.25f)
                return true;

            for (var i = 0; i < layout.Bridges.Count; i++)
            {
                var br = layout.Bridges[i];
                if (br != null && Vector2.Distance(new Vector2(x, z), br.Center) < br.Length * 0.5f + 8f)
                    return true;
            }

            if (structures != null)
            {
                for (var i = 0; i < structures.Count; i++)
                {
                    var s = structures[i];
                    if (s.size.x > 140f || s.size.z > 140f)
                        continue;
                    if (x > s.min.x - 3f && x < s.max.x + 3f && z > s.min.z - 3f && z < s.max.z + 3f)
                        return true;
                }
            }

            // Kavşak: başka bir yolun şeridine/kenarına yakınlık.
            for (var r = 0; r < layout.Roads.Count && r < model.RoadProfiles.Count; r++)
            {
                if (r == roadIndex || layout.Roads[r] == null)
                    continue;
                var prof = model.RoadProfiles[r];
                var limit = RoadsidePlan.WidthFor(layout.Roads[r].Kind, layout.Roads[r].Width) * 0.5f + 5f;
                var lim2 = limit * limit;
                for (var i = 0; i < prof.Length; i += 2)
                {
                    var dx = prof[i].x - x;
                    var dz = prof[i].z - z;
                    if (dx * dx + dz * dz < lim2)
                        return true;
                }
            }

            return false;
        }
    }
}
