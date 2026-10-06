using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// "Kartal Yaylası" ek yapıları: yörük çadırları, rüzgâr türbinleri, taş ağıl, pist+hangar, uçurum gözetleme.
    /// Diğer haritalarda hiçbir şey yapmaz. <see cref="LocationBuilder"/> çağırır.
    /// </summary>
    public static class KartalYaylasiProps
    {
        /// <summary>Türbin yerleşim noktaları (test + BuildTurbines).</summary>
        public static List<Vector2> TurbinePositions(Vector2 center, int count, float radius, float phaseRadians)
        {
            var list = new List<Vector2>(Math.Max(0, count));
            if (count <= 0 || radius <= 0f)
                return list;
            for (var i = 0; i < count; i++)
            {
                var a = phaseRadians + i * (Mathf.PI * 2f / count);
                list.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            return list;
        }

        /// <summary>Merkezden dışa birim yön; orijinde sağa.</summary>
        public static Vector2 OutwardDirection(Vector2 fromOrigin)
        {
            if (fromOrigin.sqrMagnitude < 1e-8f)
                return Vector2.right;
            return fromOrigin.normalized;
        }

        public static void BuildIfApplicable(MapLayout layout, Terrain terrain, Transform parent, int seed,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            if (layout == null || terrain == null || layout.Name != MapCatalog.KartalYaylasiName)
                return;

            var root = new GameObject("[Kartal Yaylası Ekleri]");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var rng = new System.Random(seed ^ 0x4B41);
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null)
                    continue;
                try
                {
                    if (loc.Name == MapLayout.KartalObaName)
                        BuildOba(loc, terrain, root.transform, rng, lootOut, structuresOut);
                    else if (loc.Name == MapLayout.KartalTurbineName)
                        BuildTurbines(loc, terrain, root.transform, rng, structuresOut);
                    else if (loc.Name == MapLayout.KartalSheepfoldName)
                        BuildSheepfolds(loc, terrain, root.transform, rng, structuresOut);
                    else if (loc.Name == MapLayout.KartalAirstripName)
                        BuildAirstrip(loc, terrain, root.transform, rng, lootOut, structuresOut);
                    else if (loc.Name == MapLayout.KartalCliffName)
                        BuildCliffLookout(loc, terrain, root.transform, lootOut, structuresOut);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[KartalYaylasiProps] " + loc.Name + " üretilemedi: " + e.Message);
                }
            }
        }

        private static Vector3 Ground(Terrain terrain, float x, float z)
            => new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y, z);

        private static void BuildOba(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Yörük Çadırları");
            folder.transform.SetParent(parent, false);
            for (var i = 0; i < 5; i++)
            {
                var ang = i * (Mathf.PI * 2f / 5f) + (float)rng.NextDouble() * 0.2f;
                var r = loc.Radius * (0.35f + (float)rng.NextDouble() * 0.25f);
                var x = loc.Center.x + Mathf.Cos(ang) * r;
                var z = loc.Center.y + Mathf.Sin(ang) * r;
                var pos = Ground(terrain, x, z);
                var yaw = rng.Next(0, 360);
                PropFactory.Tent(folder.transform, pos, yaw, rng);
                if (i == 0)
                {
                    PropFactory.HayBale(folder.transform, pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(2.2f, 0f, 0.5f), yaw, rng);
                    lootOut?.Add(new LootSpawnPointData(pos + Vector3.up * 0.6f, LootTier.Low));
                }
            }

            structuresOut?.Add(new Bounds(Ground(terrain, loc.Center.x, loc.Center.y) + Vector3.up * 2f,
                new Vector3(loc.Radius * 2f, 4f, loc.Radius * 2f)));
        }

        private static void BuildTurbines(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng,
            List<Bounds> structuresOut)
        {
            var folder = new GameObject("Rüzgâr Türbinleri");
            folder.transform.SetParent(parent, false);
            var spots = TurbinePositions(loc.Center, 4, loc.Radius * 0.85f, (float)rng.NextDouble() * 0.4f);
            for (var i = 0; i < spots.Count; i++)
            {
                var pos = Ground(terrain, spots[i].x, spots[i].y);
                PropFactory.WindTurbine(folder.transform, pos, rng.Next(0, 360), rng);
                structuresOut?.Add(new Bounds(pos + Vector3.up * 20f, new Vector3(8f, 44f, 8f)));
            }
        }

        private static void BuildSheepfolds(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng,
            List<Bounds> structuresOut)
        {
            var folder = new GameObject("Taş Ağıllar");
            folder.transform.SetParent(parent, false);
            var center = Ground(terrain, loc.Center.x, loc.Center.y);
            var ring = StructureKit.CreateGroup(folder.transform, "Ağıl", center, Quaternion.identity);
            const float side = 18f;
            StructureKit.CreateBox(ring.transform, "DuvarN", new Vector3(0f, 0.9f, side * 0.5f), new Vector3(side, 1.8f, 0.55f),
                Quaternion.identity, MaterialId.Stone);
            StructureKit.CreateBox(ring.transform, "DuvarS", new Vector3(0f, 0.9f, -side * 0.5f), new Vector3(side, 1.8f, 0.55f),
                Quaternion.identity, MaterialId.Stone);
            StructureKit.CreateBox(ring.transform, "DuvarE", new Vector3(side * 0.5f, 0.9f, 0f), new Vector3(0.55f, 1.8f, side),
                Quaternion.identity, MaterialId.Stone);
            StructureKit.CreateBox(ring.transform, "DuvarW", new Vector3(-side * 0.5f, 0.9f, 0f), new Vector3(0.55f, 1.8f, side * 0.7f),
                Quaternion.identity, MaterialId.Stone);
            PropFactory.Fence(folder.transform, center + new Vector3(-side * 0.45f, 0f, 0f), 90f, rng);
            for (var i = 0; i < 4; i++)
                PropFactory.HayBale(folder.transform,
                    Ground(terrain, loc.Center.x + (i - 1.5f) * 3f, loc.Center.y + ((i % 2) * 2f - 1f) * 2f),
                    rng.Next(0, 360), rng);
            StructureKit.MarkStatic(ring);
            structuresOut?.Add(new Bounds(center + Vector3.up, new Vector3(side + 2f, 3f, side + 2f)));
        }

        private static void BuildAirstrip(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Kartal Pisti");
            folder.transform.SetParent(parent, false);
            var center = Ground(terrain, loc.Center.x, loc.Center.y);
            var strip = StructureKit.CreateGroup(folder.transform, "Pist", center, Quaternion.Euler(0f, 25f, 0f));
            StructureKit.CreateBox(strip.transform, "Pist", new Vector3(0f, 0.08f, 0f), new Vector3(14f, 0.16f, 90f),
                Quaternion.identity, MaterialId.Asphalt);
            StructureKit.CreateBox(strip.transform, "Çizgi", new Vector3(0f, 0.18f, 0f), new Vector3(0.4f, 0.04f, 88f),
                Quaternion.identity, MaterialId.White);
            StructureKit.MarkStatic(strip);
            structuresOut?.Add(new Bounds(center, new Vector3(20f, 1f, 96f)));

            var hangarPos = Ground(terrain, loc.Center.x - 28f, loc.Center.y + 18f);
            PropFactory.Container(folder.transform, hangarPos, 30f, rng);
            PropFactory.Container(folder.transform, hangarPos + new Vector3(8f, 0f, 2f), 30f, rng);
            PropFactory.CamoNet(folder.transform, hangarPos + new Vector3(4f, 0f, 6f), 30f, rng);
            PropFactory.AmmoCrate(folder.transform, hangarPos + new Vector3(4f, 0f, -6f), 30f, rng);
            lootOut?.Add(new LootSpawnPointData(hangarPos + Vector3.up * 0.8f, LootTier.Military));
            structuresOut?.Add(new Bounds(hangarPos + Vector3.up * 3f, new Vector3(16f, 8f, 20f)));
        }

        private static void BuildCliffLookout(LocationSpec loc, Terrain terrain, Transform parent,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            var pos = Ground(terrain, loc.Center.x, loc.Center.y);
            var tower = StructureKit.CreateGroup(parent, "Uçurum Gözetleme", pos, Quaternion.identity);
            StructureKit.CreateBox(tower.transform, "Kaide", new Vector3(0f, 0.6f, 0f), new Vector3(4.5f, 1.2f, 4.5f),
                Quaternion.identity, MaterialId.Stone);
            StructureKit.CreateBox(tower.transform, "Gövde", new Vector3(0f, 3.5f, 0f), new Vector3(3.2f, 5f, 3.2f),
                Quaternion.identity, MaterialId.WoodDark);
            StructureKit.CreateBox(tower.transform, "Çatı", new Vector3(0f, 6.4f, 0f), new Vector3(4f, 0.35f, 4f),
                Quaternion.identity, MaterialId.MetalDark);
            StructureKit.CreateCylinder(tower.transform, "Dürbün", new Vector3(0.9f, 7.1f, 0.6f), 0.18f, 0.9f, MaterialId.MetalPanel);
            StructureKit.MarkStatic(tower);
            lootOut?.Add(new LootSpawnPointData(pos + Vector3.up * 6.5f, LootTier.High));
            structuresOut?.Add(new Bounds(pos + Vector3.up * 4f, new Vector3(6f, 9f, 6f)));
        }
    }
}
