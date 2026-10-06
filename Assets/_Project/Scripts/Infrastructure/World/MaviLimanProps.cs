using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// "Mavi Liman" ek yapıları: liman iskelesi + nakliye konteynerleri (Liman Depoları), deniz feneri kulesi (Deniz Feneri)
    /// ve zeytinlik ağaçları + taş teras duvarları (Zeytinlik). Mevcut PropFactory / StructureKit ilkelleriyle prosedürel üretilir;
    /// diğer haritalarda hiçbir şey yapmaz.
    /// </summary>
    public static class MaviLimanProps
    {
        public const string HarborName = "Liman Depoları";
        public const string LighthouseName = "Deniz Feneri";
        public const string GroveName = "Zeytinlik";

        public static void BuildIfApplicable(MapLayout layout, Terrain terrain, Transform parent, int seed,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            if (layout == null || terrain == null || layout.Name != MapCatalog.MaviLimanName)
                return;

            var root = new GameObject("[Mavi Liman Ekleri]");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var rng = new System.Random(seed ^ 0x3A71);
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null)
                    continue;
                try
                {
                    if (loc.Name == HarborName)
                        BuildHarbor(layout, loc, terrain, root.transform, rng, lootOut, structuresOut);
                    else if (loc.Name == LighthouseName)
                        BuildLighthouse(loc, terrain, root.transform, structuresOut);
                    else if (loc.Name == GroveName)
                        BuildOliveGrove(loc, terrain, root.transform, rng, lootOut);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MaviLimanProps] " + loc.Name + " üretilemedi: " + e.Message);
                }
            }
        }

        /// <summary>
        /// (from) noktasından (dir) yönünde ilerleyip yüksekliğin su seviyesinin altına düştüğü ilk mesafeyi döner;
        /// bulunamazsa -1. Saf mantık (test edilebilir).
        /// </summary>
        public static float FindShoreDistance(Func<float, float, float> height, Vector2 from, Vector2 dir, float waterLevel,
            float maxDistance, float step = 2f)
        {
            if (height == null || dir.sqrMagnitude < 1e-6f)
                return -1f;
            dir.Normalize();
            for (var d = 0f; d <= maxDistance; d += step)
            {
                var p = from + dir * d;
                if (height(p.x, p.y) < waterLevel + 0.3f)
                    return d;
            }

            return -1f;
        }

        private static Vector3 Ground(Terrain terrain, float x, float z)
            => new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y, z);

        // ------------------------------------------------------------------ Liman

        private static void BuildHarbor(MapLayout layout, LocationSpec loc, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Liman İskelesi");
            folder.transform.SetParent(parent, false);

            // Deniz yönü: en yakın göl merkezi (deniz diski).
            var dir = Vector2.right;
            var best = float.MaxValue;
            for (var i = 0; i < layout.Lakes.Count; i++)
            {
                var d = (layout.Lakes[i].Center - loc.Center).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    dir = (layout.Lakes[i].Center - loc.Center).normalized;
                }
            }

            var shore = FindShoreDistance((x, z) => Ground(terrain, x, z).y, loc.Center, dir, layout.WaterLevel, 260f);
            var yaw = StructureKit.YawOf(new Vector3(dir.x, 0f, dir.y));
            var start = shore > 0f ? loc.Center + dir * Mathf.Max(0f, shore - 14f) : loc.Center + dir * (loc.Radius * 0.8f);
            var deckY = layout.WaterLevel + 1.1f;
            const float length = 42f;
            var mid = start + dir * (length * 0.5f);
            var pier = StructureKit.CreateGroup(folder.transform, "İskele", new Vector3(mid.x, deckY, mid.y), Quaternion.Euler(0f, yaw, 0f));
            StructureKit.CreateBox(pier.transform, "Tabliye", Vector3.zero, new Vector3(7f, 0.5f, length), Quaternion.identity, MaterialId.Concrete);
            for (var i = 0; i < 6; i++)
            {
                var z = -length * 0.5f + 3f + i * (length - 6f) / 5f;
                StructureKit.CreateCylinder(pier.transform, "Kazık" + i + "a", new Vector3(-3.2f, -2f, z), 0.35f, 5f, MaterialId.WoodDark);
                StructureKit.CreateCylinder(pier.transform, "Kazık" + i + "b", new Vector3(3.2f, -2f, z), 0.35f, 5f, MaterialId.WoodDark);
                if (i % 2 == 0)
                    StructureKit.CreateCylinder(pier.transform, "Babalı" + i, new Vector3(3.0f, 0.55f, z), 0.28f, 0.6f, MaterialId.MetalDark, false);
            }

            StructureKit.MarkStatic(pier);
            structuresOut?.Add(new Bounds(new Vector3(mid.x, deckY, mid.y), new Vector3(9f, 3f, length)));

            // Rıhtım üstü: konteynerler, variller, kasalar.
            var side = new Vector2(-dir.y, dir.x);
            for (var i = 0; i < 4; i++)
            {
                var p = start - dir * (4f + i * 6f) + side * (10f + (i % 2) * 7f);
                PropFactory.Container(folder.transform, Ground(terrain, p.x, p.y), yaw + (i % 2) * 90f, rng);
            }

            for (var i = 0; i < 3; i++)
            {
                var p = start + dir * (10f + i * 9f) + side * 2.5f;
                PropFactory.Barrel(folder.transform, new Vector3(p.x, deckY + 0.3f, p.y), rng.Next(0, 360), rng);
            }

            PropFactory.AmmoCrate(folder.transform, new Vector3(start.x + dir.x * 4f, deckY + 0.3f, start.y + dir.y * 4f), yaw, rng);
            lootOut?.Add(new LootSpawnPointData(new Vector3(start.x + dir.x * 4f, deckY + 0.8f, start.y + dir.y * 4f), LootTier.Military));
        }

        // ------------------------------------------------------------------ Fener

        private static void BuildLighthouse(LocationSpec loc, Terrain terrain, Transform parent, List<Bounds> structuresOut)
        {
            var pos = Ground(terrain, loc.Center.x + 9f, loc.Center.y + 7f);
            var root = StructureKit.CreateGroup(parent, "Deniz Feneri Kulesi", pos, Quaternion.identity);
            const float baseH = 3f;
            StructureKit.CreateCylinder(root.transform, "Kaide", new Vector3(0f, baseH * 0.5f, 0f), 4.2f, baseH, MaterialId.Stone);
            var y = baseH;
            for (var i = 0; i < 5; i++)
            {
                var r = 3.3f - i * 0.25f;
                StructureKit.CreateCylinder(root.transform, "Gövde" + i, new Vector3(0f, y + 2.5f, 0f), r, 5f,
                    i % 2 == 0 ? MaterialId.Plaster : MaterialId.Red);
                y += 5f;
            }

            StructureKit.CreateCylinder(root.transform, "Balkon", new Vector3(0f, y + 0.2f, 0f), 3.6f, 0.4f, MaterialId.MetalDark);
            StructureKit.CreateCylinder(root.transform, "Fener", new Vector3(0f, y + 1.6f, 0f), 1.5f, 2.4f, MaterialId.Glass, false);
            StructureKit.CreateCylinder(root.transform, "Kubbe", new Vector3(0f, y + 3.2f, 0f), 1.9f, 0.8f, MaterialId.MetalDark, false);
            StructureKit.MarkStatic(root);
            structuresOut?.Add(new Bounds(pos + Vector3.up * 15f, new Vector3(9f, 32f, 9f)));
        }

        // ------------------------------------------------------------------ Zeytinlik

        private static void BuildOliveGrove(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut)
        {
            var folder = new GameObject("Zeytinlik Ağaçları");
            folder.transform.SetParent(parent, false);
            const int rows = 7;
            const int cols = 7;
            var spacing = loc.Radius * 1.5f / rows;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var x = loc.Center.x + (c - (cols - 1) * 0.5f) * spacing + ((float)rng.NextDouble() - 0.5f) * 3f;
                    var z = loc.Center.y + (r - (rows - 1) * 0.5f) * spacing + ((float)rng.NextDouble() - 0.5f) * 3f;
                    if ((new Vector2(x, z) - loc.Center).magnitude > loc.Radius * 0.85f || rng.NextDouble() < 0.12)
                        continue;
                    var p = Ground(terrain, x, z);
                    var h = 2.2f + (float)rng.NextDouble() * 0.8f;
                    var tree = StructureKit.CreateGroup(folder.transform, "Zeytin", p, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                    StructureKit.CreateCylinder(tree.transform, "Gövde", new Vector3(0f, h * 0.5f, 0f), 0.28f, h, MaterialId.Bark);
                    StructureKit.CreateCylinder(tree.transform, "Taç", new Vector3(0.2f, h + 0.5f, 0f), 1.9f + (float)rng.NextDouble() * 0.5f, 1.8f,
                        MaterialId.FoliageDark, false);
                    StructureKit.MarkStatic(tree);
                }
            }

            // Taş teras duvarları (rotasyonu böler).
            for (var i = 0; i < 3; i++)
            {
                var z = loc.Center.y + (i - 1) * loc.Radius * 0.55f;
                var p = Ground(terrain, loc.Center.x - loc.Radius * 0.2f, z);
                var wall = StructureKit.CreateBox(folder.transform, "TeraDuvarı" + i, p + Vector3.up * 0.6f,
                    new Vector3(loc.Radius * 0.9f, 1.2f, 0.7f), Quaternion.identity, MaterialId.Stone);
                StructureKit.MarkStatic(wall);
            }

            // Bakım kulübesi: yağma küçük kulübelerde.
            var hutPos = Ground(terrain, loc.Center.x + loc.Radius * 0.35f, loc.Center.y - loc.Radius * 0.3f);
            var hut = BuildingGenerator.Build(new BuildingSpec
            {
                Name = "Bakım Kulübesi",
                Style = BuildingStyle.ShepherdHut,
                Position = hutPos,
                Yaw = rng.Next(0, 360),
                Width = 5f,
                Depth = 4f,
                Tier = LootTier.Low,
                Seed = rng.Next()
            }, folder.transform);
            if (hut?.LootPoints != null)
            {
                for (var i = 0; i < hut.LootPoints.Count; i++)
                    lootOut?.Add(new LootSpawnPointData(hut.LootPoints[i], LootTier.Low));
            }
        }
    }
}
