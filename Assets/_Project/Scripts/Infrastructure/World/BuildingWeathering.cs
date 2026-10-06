using System;
using System.Collections.Generic;
using UnityEngine;
using M = Project.Infrastructure.Rendering.MaterialId;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Bina yıpranması / köy detayları dış API'si:
    /// <list type="bullet">
    /// <item><see cref="Tier"/>: kalite kademesi (QualityTierApplier.Apply -> <see cref="SetTier"/>); BuildingGenerator her binayı bu kademeyle üretir
    /// (dünya kurulurken uygulanmış kademe geçerli olur; kademe değişince yeni üretilen binalar etkilenir).</item>
    /// <item><see cref="BuildPowerLines"/>: köydeki bina "ElektrikGirisi" noktaları arasında direk + sarkık kablo üretir.</item>
    /// </list>
    /// </summary>
    public static class BuildingWeathering
    {
        /// <summary>BuildingGenerator'ın her bina köküne eklediği boş çocuk (elektrik bağlantı noktası, yerel konum).</summary>
        public const string PowerDropName = "ElektrikGirisi";

        private const float PoleHeight = 7.2f;
        private const float PoleRadius = 0.11f;

        private static int _tier = 2;

        /// <summary>Kademe 0 Düşük … 3 Ultra. Varsayılan 2 (Yüksek).</summary>
        public static int Tier => _tier;

        /// <summary>QualityTierApplier'dan çağrılır.</summary>
        public static void SetTier(int tier) => _tier = Mathf.Clamp(tier, 0, 3);

        /// <summary>Binanın elektrik giriş noktası (dünya uzayı). Yoksa false.</summary>
        public static bool TryGetPowerDrop(BuildingResult building, out Vector3 world)
        {
            world = default;
            if (building == null || building.Root == null)
                return false;
            var t = building.Root.transform.Find(PowerDropName);
            if (t == null)
                return false;
            world = t.position;
            return true;
        }

        /// <summary>
        /// Bina girişlerini (ElektrikGirisi) en kısa-ağaç ile bağlayan direk ve sarkık kablo hattı üretir. Binalara ve diğer yapılara çarpmayan
        /// direk konumu aranır. Dönen kök, parent altındadır (Default katman, statik). Hiç giriş yoksa null.
        /// </summary>
        /// <param name="buildings">Üretilmiş binalar (sınırlar direklerin içeri girmesini önlemek için kullanılır).</param>
        /// <param name="parent">Üst nesne (null olabilir).</param>
        /// <param name="groundHeight">(x, z) -> arazi yüksekliği; null ise aşağı ışın, o da olmazsa giriş yüksekliği - 3.</param>
        public static GameObject BuildPowerLines(IReadOnlyList<BuildingResult> buildings, Transform parent, Func<float, float, float> groundHeight = null,
            float poleSpan = BuildingWeatheringMath.DefaultPoleSpan, float maxLink = BuildingWeatheringMath.DefaultMaxLink)
        {
            if (buildings == null || buildings.Count == 0)
                return null;
            var anchors = new List<Vector3>(buildings.Count);
            for (var i = 0; i < buildings.Count; i++)
            {
                if (TryGetPowerDrop(buildings[i], out var w))
                    anchors.Add(w);
            }

            if (anchors.Count == 0)
                return null;

            var nodes = new List<GridNode>(anchors.Count + 8);
            var spans = new List<GridSpan>(anchors.Count + 8);
            BuildingWeatheringMath.PlanGrid(anchors, maxLink, poleSpan, nodes, spans);
            if (spans.Count == 0)
                return null;

            var root = StructureKit.CreateGroup(parent, "ElektrikHatti", Vector3.zero, Quaternion.identity);

            // Direk konumları (binalardan uzak, arazi üstünde) ve çapraz kol ekseni
            var pos = new Vector3[nodes.Count];
            var ground = new float[nodes.Count];
            var axis = new Vector3[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                pos[i] = nodes[i].Position;
                if (!nodes[i].IsPole)
                    continue;
                pos[i] = FreeSpot(nodes[i].Position, buildings);
                ground[i] = GroundAt(pos[i].x, pos[i].z, groundHeight, nodes[i].Position.y - 3f);
                pos[i].y = ground[i] + PoleHeight;
                axis[i] = Vector3.right;
            }

            for (var s = 0; s < spans.Count; s++)
            {
                var sp = spans[s];
                if (nodes[sp.A].IsPole)
                    axis[sp.A] = CrossAxis(pos[sp.A], pos[sp.B]);
                if (nodes[sp.B].IsPole)
                    axis[sp.B] = CrossAxis(pos[sp.B], pos[sp.A]);
            }

            // Bileşen başına ayrı ağ (sınırlar küçük kalsın; bütün köy tek ağ olmasın)
            var comp = new int[nodes.Count];
            for (var i = 0; i < comp.Length; i++)
                comp[i] = i;
            for (var s = 0; s < spans.Count; s++)
            {
                var a = Find(comp, spans[s].A);
                var b = Find(comp, spans[s].B);
                if (a != b)
                    comp[a] = b;
            }

            var groups = new Dictionary<int, StructureBuilder>();
            var tier = _tier;
            var cables = BuildingWeatheringMath.CablesPerSpan(tier);

            for (var i = 0; i < nodes.Count; i++)
            {
                if (!nodes[i].IsPole)
                    continue;
                var b = Builder(groups, Find(comp, i));
                BuildPole(b, pos[i], ground[i], axis[i]);
            }

            for (var s = 0; s < spans.Count; s++)
            {
                var sp = spans[s];
                var b = Builder(groups, Find(comp, sp.A));
                var bothPoles = nodes[sp.A].IsPole && nodes[sp.B].IsPole;
                var lines = bothPoles ? cables : 1;
                for (var k = 0; k < lines; k++)
                {
                    var slot = lines == 1 ? 1 : (lines == 2 ? k * 2 : k);
                    var pa = Attach(nodes[sp.A], pos[sp.A], axis[sp.A], slot);
                    var pb = Attach(nodes[sp.B], pos[sp.B], axis[sp.B], slot);
                    Cable(b, pa, pb, tier);
                }
            }

            foreach (var kv in groups)
            {
                var sub = StructureKit.CreateGroup(root.transform, "Hat" + kv.Key, Vector3.zero, Quaternion.identity);
                kv.Value.BuildInto(sub.transform);
            }

            StructureKit.MarkStatic(root);
            return root;
        }

        private static StructureBuilder Builder(Dictionary<int, StructureBuilder> groups, int key)
        {
            if (!groups.TryGetValue(key, out var b))
            {
                b = new StructureBuilder();
                groups[key] = b;
            }

            return b;
        }

        private static int Find(int[] comp, int i)
        {
            while (comp[i] != i)
            {
                comp[i] = comp[comp[i]];
                i = comp[i];
            }

            return i;
        }

        private static Vector3 CrossAxis(Vector3 from, Vector3 to)
        {
            var d = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (d.sqrMagnitude < 0.0001f)
                return Vector3.right;
            d.Normalize();
            return new Vector3(-d.z, 0f, d.x);
        }

        private static Vector3 Attach(GridNode node, Vector3 pos, Vector3 axis, int slot)
        {
            if (!node.IsPole)
                return pos;
            return pos + axis * ((slot - 1) * 0.7f) + Vector3.down * 0.18f;
        }

        private static float GroundAt(float x, float z, Func<float, float, float> groundHeight, float fallback)
        {
            if (groundHeight != null)
                return groundHeight(x, z);
            if (Physics.Raycast(new Vector3(x, 600f, z), Vector3.down, out var hit, 1200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        /// <summary>Direk noktasını binaların (2 m pay) dışına iter: 3/6/9 m'de dört yöne dener.</summary>
        private static Vector3 FreeSpot(Vector3 p, IReadOnlyList<BuildingResult> buildings)
        {
            if (!Blocked(p, buildings))
                return p;
            for (var r = 3f; r <= 9.1f; r += 3f)
            {
                for (var a = 0; a < 8; a++)
                {
                    var ang = a * Mathf.PI * 0.25f;
                    var q = p + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    if (!Blocked(q, buildings))
                        return q;
                }
            }

            return p;
        }

        private static bool Blocked(Vector3 p, IReadOnlyList<BuildingResult> buildings)
        {
            for (var i = 0; i < buildings.Count; i++)
            {
                var bnd = buildings[i].Bounds;
                bnd.Expand(new Vector3(4f, 100f, 4f));
                if (bnd.Contains(new Vector3(p.x, bnd.center.y, p.z)))
                    return true;
            }

            return false;
        }

        private static void BuildPole(StructureBuilder b, Vector3 top, float groundY, Vector3 axis)
        {
            var x = top.x;
            var z = top.z;
            b.VerticalCylinder(new Vector3(x, groundY - 0.25f, z), PoleRadius, PoleHeight + 0.25f, 8, M.WoodDark, StructureCollider.None);
            b.ColliderBox(new Vector3(x, groundY + PoleHeight * 0.5f, z), new Vector3(PoleRadius * 2f, PoleHeight, PoleRadius * 2f), Quaternion.identity, M.WoodDark);
            var arm = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up), Vector3.up);
            var armY = top.y - 0.3f;
            b.Box(new Vector3(x, armY, z), new Vector3(0.09f, 0.1f, 1.7f), Quaternion.LookRotation(axis, Vector3.up), M.WoodDark, StructureCollider.None);
            // Köşebent (diyagonal destek)
            b.Beam(new Vector3(x, armY - 0.6f, z) + axis * 0.05f, new Vector3(x, armY, z) + axis * 0.6f, 0.05f, 0.06f, M.WoodDark);
            b.Beam(new Vector3(x, armY - 0.6f, z) - axis * 0.05f, new Vector3(x, armY, z) - axis * 0.6f, 0.05f, 0.06f, M.WoodDark);
            for (var k = -1; k <= 1; k++)
            {
                var ip = new Vector3(x, armY + 0.08f, z) + axis * (k * 0.7f);
                b.VerticalCylinder(ip, 0.035f, 0.1f, 6, M.Gray, StructureCollider.None);
            }

            // Basit trafo kutusu / sigorta (görsel)
            b.Box(new Vector3(x, armY - 1.1f, z) + Vector3.Cross(axis, Vector3.up) * 0.2f, new Vector3(0.22f, 0.3f, 0.12f), arm, M.MetalDark, StructureCollider.None);
        }

        private static void Cable(StructureBuilder b, Vector3 a, Vector3 c, int tier)
        {
            var len = Vector3.Distance(a, c);
            if (len < 0.5f)
                return;
            var seg = BuildingWeatheringMath.CableSegments(len, tier);
            var sag = BuildingWeatheringMath.SagFor(len, 1f);
            var prev = a;
            for (var i = 1; i <= seg; i++)
            {
                var t = i / (float)seg;
                var p = Vector3.Lerp(a, c, t);
                p.y = BuildingWeatheringMath.SagY(a.y, c.y, t, sag);
                b.Beam(prev, p, 0.022f, 0.022f, M.Black, StructureCollider.None);
                prev = p;
            }
        }
    }
}
