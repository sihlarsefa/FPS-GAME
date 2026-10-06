using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Yol kenarı detayları: ana yolda ahşap elektrik direkleri + sarkan teller, km taşları, uçurum kenarında korkuluk.
    /// Yol ekseni TerrainModel.RoadProfiles'tan (pürüzsüz Catmull-Rom) okunur. Tek birleşik mesh + az sayıda kutu çarpıştırıcı
    /// (prop bütçesi RoadsidePlan.Max*). Deterministik (RNG yok); köprü, su, kavşak ve yapılardan kaçınır.
    /// </summary>
    public static class RoadsideDetails
    {
        public const string RootName = "YolDetayları";
        private const int SubWood = 0, SubConcrete = 1, SubMetal = 2, SubWire = 3, SubRed = 4;
        private const float PoleHeight = 7.2f;
        private const int WireSegments = 6;

        public static int Build(MapLayout layout, TerrainModel model, Terrain terrain, Transform parent,
            IReadOnlyList<Bounds> structures)
        {
            if (layout == null || model == null || layout.Roads == null || layout.Roads.Count == 0)
                return 0;

            var yOffset = terrain != null ? terrain.transform.position.y : 0f;
            var builder = new MeshBuilder(5);
            var root = new GameObject(RootName) { layer = GameLayers.Default };
            if (parent != null)
                root.transform.SetParent(parent, false);

            var poles = 0;
            var stones = 0;
            var runs = 0;
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
                        Mathf.Max(0, RoadsidePlan.MaxPoles - poles), blocked);
                    BuildPoles(builder, root, plan, terrain, yOffset, model);
                    poles += plan.Count;

                    var km = RoadsidePlan.PlanKmStones(samples, width,
                        Mathf.Max(0, RoadsidePlan.MaxKmStones - stones), blocked);
                    for (var i = 0; i < km.Count; i++)
                        AddKmStone(builder, root, km[i], terrain, yOffset);
                    stones += km.Count;
                }

                var rails = RoadsidePlan.PlanGuardrails(samples, width,
                    Mathf.Max(0, RoadsidePlan.MaxGuardRuns - runs), model.SampleHeight, blocked);
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
                MaterialLibrary.Get(MaterialId.Red)
            };
            StructureKit.MarkStatic(root);
            return poles + stones + runs;
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
                var p = Vector3.Lerp(a, c, t) + Vector3.down * RoadsidePlan.WireSag(t, sag);
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

        private static void AddGuardrail(MeshBuilder b, GameObject root, GuardRun run, int index)
        {
            var d = run.End - run.Start;
            var len = d.magnitude;
            if (len < 0.5f)
                return;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir);
            var mid = (run.Start + run.End) * 0.5f;
            MeshFactory.AddBox(b, SubMetal, mid + Vector3.up * 0.7f, new Vector3(0.08f, 0.3f, len), rot);
            var posts = Mathf.Max(2, Mathf.CeilToInt(len / 4f) + 1);
            for (var i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(run.Start, run.End, i / (float)(posts - 1));
                MeshFactory.AddBox(b, SubWood, p + Vector3.up * 0.45f, new Vector3(0.12f, 0.9f, 0.12f), rot);
            }

            var go = new GameObject("Korkuluk" + index) { layer = GameLayers.Default };
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(mid + Vector3.up * 0.6f, rot);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.2f, 1.1f, len);
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
