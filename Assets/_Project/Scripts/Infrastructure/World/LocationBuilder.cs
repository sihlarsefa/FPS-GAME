using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.World.Lighting;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Harita lokasyonlarını bina + prop ile doldurur.</summary>
    public static class LocationBuilder
    {
        public static void BuildAll(
            MapLayout layout,
            Terrain terrain,
            Transform parent,
            int seed,
            List<LootSpawnPointData> lootOut,
            List<Bounds> structuresOut,
            List<VehicleSpawnData> vehiclesOut)
        {
            if (layout == null)
                return;

            lootOut ??= new List<LootSpawnPointData>();
            structuresOut ??= new List<Bounds>();
            vehiclesOut ??= new List<VehicleSpawnData>();

            PoiLights.ResetBudget();
            var probes = new List<ProbeSpec>();
            var root = new GameObject("[Lokasyonlar]");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var rng = new System.Random(seed ^ 0x4C0C);
            var locations = layout.Locations;
            if (locations != null)
            {
                for (var i = 0; i < locations.Count; i++)
                {
                    var loc = locations[i];
                    if (loc == null)
                        continue;
                    BuildLocation(loc, terrain, root.transform, rng.Next(), lootOut, structuresOut, vehiclesOut, probes);
                }
            }

            ScatterRoadside(layout, terrain, root.transform, rng, lootOut);
            MaviLimanProps.BuildIfApplicable(layout, terrain, root.transform, seed, lootOut, structuresOut);
            KartalYaylasiProps.BuildIfApplicable(layout, terrain, root.transform, seed, lootOut, structuresOut);
            MapIdentityProps.BuildIfApplicable(layout, terrain, root.transform, seed, lootOut, structuresOut);
            PoiDetailProps.BuildIfApplicable(layout, terrain, root.transform, seed, lootOut, structuresOut);

            if (probes.Count > 0)
                PoiLights.BuildProbes(root.transform, probes, Vector3.zero);
        }

        private static void BuildLocation(
            LocationSpec loc,
            Terrain terrain,
            Transform parent,
            int seed,
            List<LootSpawnPointData> lootOut,
            List<Bounds> structuresOut,
            List<VehicleSpawnData> vehiclesOut,
            List<ProbeSpec> probes)
        {
            // Kimlik POI'leri (donmuş göl, konteyner sahası, telesiyej...) MapIdentityProps tarafından özel kurulur.
            if (MapLayout.IsIdentityPoi(loc.Name))
                return;

            var folder = new GameObject(loc.Name ?? loc.Kind.ToString());
            folder.transform.SetParent(parent, false);
            var rng = new System.Random(seed);
            var center = GroundPoint(terrain, loc.Center.x, loc.Center.y);

            switch (loc.Kind)
            {
                case LocationKind.Village:
                    BuildVillage(loc, center, terrain, folder.transform, rng, lootOut, structuresOut, vehiclesOut);
                    break;
                case LocationKind.Karakol:
                    BuildKarakol(loc, center, terrain, folder.transform, rng, lootOut, structuresOut, vehiclesOut);
                    break;
                case LocationKind.ForwardBase:
                    BuildForwardBase(loc, center, terrain, folder.transform, rng, lootOut, structuresOut, vehiclesOut);
                    break;
                case LocationKind.Quarry:
                    BuildQuarry(loc, center, terrain, folder.transform, rng, lootOut, structuresOut);
                    break;
                case LocationKind.Dam:
                    BuildDam(loc, center, terrain, folder.transform, rng, lootOut, structuresOut);
                    break;
                case LocationKind.RelayHill:
                    BuildRelay(loc, center, terrain, folder.transform, rng, lootOut, structuresOut);
                    break;
                case LocationKind.Farm:
                    BuildFarm(loc, center, terrain, folder.transform, rng, lootOut, structuresOut, vehiclesOut);
                    break;
                case LocationKind.Ruins:
                    BuildRuins(loc, center, terrain, folder.transform, rng, lootOut, structuresOut);
                    break;
                case LocationKind.Outpost:
                    BuildOutpost(loc, center, terrain, folder.transform, rng, lootOut, structuresOut);
                    break;
                case LocationKind.Forest:
                    PropFactory.TreeStump(folder.transform, center, rng.Next(0, 360), rng);
                    PropFactory.Woodpile(folder.transform, OffsetGround(terrain, center, 4f, -3f), 20f, rng);
                    break;
            }

            // Gece aktif yerel ışıklar + reflection probe (gündüz kapalı, bütçe PoiLights içinde).
            var site = SiteFor(loc.Kind);
            if (site.HasValue)
            {
                var rad = Mathf.Max(12f, loc.Radius);
                PoiLights.Build(folder.transform, site.Value, center, rad, seed ^ 0x11A7, center);
                probes.Add(ReflectionProbePlanner.ForPoi(center, rad));
            }
        }

        private static PoiSiteType? SiteFor(LocationKind k)
        {
            switch (k)
            {
                case LocationKind.Village: return PoiSiteType.Village;
                case LocationKind.Karakol: return PoiSiteType.Checkpoint;
                case LocationKind.ForwardBase: return PoiSiteType.MilitaryBase;
                case LocationKind.Outpost: return PoiSiteType.Camp;
                default: return null;
            }
        }

        private static void BuildVillage(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut, List<VehicleSpawnData> vehiclesOut)
        {
            var count = 10 + rng.Next(0, 7);
            for (var i = 0; i < count; i++)
            {
                var a = (i / (float)count) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.3f;
                var r = 12f + (float)rng.NextDouble() * Mathf.Max(18f, loc.Radius * 0.55f);
                var pos = OffsetGround(terrain, center, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                var style = rng.NextDouble() > 0.7 ? BuildingStyle.TwoStoryHouse : BuildingStyle.VillageHouse;
                PlaceBuilding(parent, "Ev_" + i, style, pos, a * Mathf.Rad2Deg, 7f + rng.Next(0, 3), 6f + rng.Next(0, 2),
                    style == BuildingStyle.TwoStoryHouse ? 2 : 1, loc.Tier, rng.Next(), false, lootOut, structuresOut);
            }

            PlaceBuilding(parent, "Cami", BuildingStyle.Mosque, OffsetGround(terrain, center, 2f, 4f), 0f,
                12f, 12f, 1, loc.Tier, rng.Next(), false, lootOut, structuresOut);
            PropFactory.Well(parent, OffsetGround(terrain, center, -6f, 2f), 0f, rng);
            PropFactory.StreetLamp(parent, OffsetGround(terrain, center, 8f, -4f), 15f, rng);
            PropFactory.Fence(parent, OffsetGround(terrain, center, -14f, 10f), 90f, rng);
            vehiclesOut.Add(new VehicleSpawnData(OffsetGround(terrain, center, 18f, -8f), 40f));
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, 0f, -10f), loc.Tier));
        }

        private static void BuildKarakol(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut, List<VehicleSpawnData> vehiclesOut)
        {
            PlaceBuilding(parent, "Karakol", BuildingStyle.Karakol, center, 0f, 14f, 12f, 2, LootTier.High, rng.Next(),
                false, lootOut, structuresOut);

            var wall = 22f;
            for (var i = 0; i < 4; i++)
            {
                var yaw = i * 90f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                PropFactory.ConcreteBarrier(parent, center + dir * wall * 0.5f, yaw, rng);
                PropFactory.Hesco(parent, center + dir * (wall * 0.5f - 2f) + Vector3.right * 3f, yaw, rng);
            }

            PlaceBuilding(parent, "KuleKuzey", BuildingStyle.WatchTower, OffsetGround(terrain, center, 0f, wall * 0.45f), 0f,
                4f, 4f, 2, LootTier.High, rng.Next(), false, lootOut, structuresOut);
            PlaceBuilding(parent, "KuleGuney", BuildingStyle.WatchTower, OffsetGround(terrain, center, 0f, -wall * 0.45f), 180f,
                4f, 4f, 2, LootTier.High, rng.Next(), false, lootOut, structuresOut);
            PropFactory.FlagPole(parent, OffsetGround(terrain, center, 6f, 6f), 0f, rng);
            PropFactory.GuardBooth(parent, OffsetGround(terrain, center, 0f, -wall * 0.55f), 0f, rng);
            PropFactory.Sandbags(parent, OffsetGround(terrain, center, -8f, 8f), 45f, rng);
            vehiclesOut.Add(new VehicleSpawnData(OffsetGround(terrain, center, 12f, -14f), 180f));
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, -4f, 2f), LootTier.Military));
        }

        private static void BuildForwardBase(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut, List<VehicleSpawnData> vehiclesOut)
        {
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f * Mathf.Deg2Rad;
                PropFactory.Hesco(parent, OffsetGround(terrain, center, Mathf.Cos(a) * 28f, Mathf.Sin(a) * 28f),
                    i * 45f, rng);
            }

            for (var i = 0; i < 4; i++)
                PropFactory.Tent(parent, OffsetGround(terrain, center, -10f + i * 7f, 8f), 0f, rng);

            PropFactory.Container(parent, OffsetGround(terrain, center, 14f, 4f), 90f, rng);
            PropFactory.Container(parent, OffsetGround(terrain, center, 14f, -4f), 90f, rng);
            PropFactory.Helipad(parent, OffsetGround(terrain, center, -5f, -16f), 0f, rng);
            PropFactory.AmmoCrate(parent, OffsetGround(terrain, center, 4f, 2f), 10f, rng);
            PropFactory.AmmoCrate(parent, OffsetGround(terrain, center, 6f, 2f), -5f, rng);
            PropFactory.CamoNet(parent, OffsetGround(terrain, center, 0f, 12f), 0f, rng);
            PropFactory.FlagPole(parent, OffsetGround(terrain, center, -12f, -2f), 0f, rng);
            PlaceBuilding(parent, "Kışla", BuildingStyle.Barracks, OffsetGround(terrain, center, 8f, -8f), 15f,
                16f, 10f, 1, LootTier.Military, rng.Next(), false, lootOut, structuresOut);

            vehiclesOut.Add(new VehicleSpawnData(OffsetGround(terrain, center, 20f, 10f), 270f));
            vehiclesOut.Add(new VehicleSpawnData(OffsetGround(terrain, center, 20f, 16f), 270f));
            // Helipad üzerindeki uçurulabilir T-70 (MatchBootstrap IsAir noktalarını helikopter olarak üretir).
            vehiclesOut.Add(new VehicleSpawnData(OffsetGround(terrain, center, -5f, -16f) + Vector3.up * 0.15f, 0f, true));
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, 0f, 0f), LootTier.Military));
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, 5f, -5f), LootTier.Military));
        }

        private static void BuildQuarry(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            for (var i = 0; i < 8; i++)
                PropFactory.Rock(parent, OffsetGround(terrain, center, (i % 4) * 6f - 9f, (i / 4) * 8f - 4f), i * 20f, rng);
            PropFactory.Wreck(parent, OffsetGround(terrain, center, 10f, -6f), 35f, rng);
            PlaceBuilding(parent, "Baraka", BuildingStyle.Shed, OffsetGround(terrain, center, -8f, 6f), 10f,
                6f, 5f, 1, loc.Tier, rng.Next(), false, lootOut, structuresOut);
            PropFactory.Barrel(parent, OffsetGround(terrain, center, 2f, 3f), 0f, rng);
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, -2f, 2f), loc.Tier));
        }

        private static void BuildDam(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            StructureKit.CreateBox(parent, "BarajDuvarı", center + Vector3.up * 4f,
                new Vector3(48f, 8f, 4f), Quaternion.identity, Rendering.MaterialId.Concrete);
            PlaceBuilding(parent, "BarajKontrol", BuildingStyle.DamControl, OffsetGround(terrain, center, 0f, 8f), 0f,
                10f, 8f, 2, loc.Tier, rng.Next(), false, lootOut, structuresOut);
            PropFactory.Antenna(parent, OffsetGround(terrain, center, 12f, 10f), 0f, rng);
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, -4f, 6f), loc.Tier));
            structuresOut.Add(new Bounds(center + Vector3.up * 4f, new Vector3(48f, 8f, 4f)));
        }

        private static void BuildRelay(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            PropFactory.Antenna(parent, center, 0f, rng);
            PropFactory.Antenna(parent, OffsetGround(terrain, center, 6f, 2f), 20f, rng);
            PlaceBuilding(parent, "Bunker", BuildingStyle.Bunker, OffsetGround(terrain, center, -5f, -4f), 30f,
                8f, 7f, 1, LootTier.High, rng.Next(), false, lootOut, structuresOut);
            PlaceBuilding(parent, "Radar", BuildingStyle.RadarStation, OffsetGround(terrain, center, 4f, -6f), 0f,
                6f, 6f, 1, LootTier.High, rng.Next(), false, lootOut, structuresOut);
            PropFactory.SandbagRing(parent, OffsetGround(terrain, center, 0f, 0f), 0f, rng, 5f);
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, -2f, -2f), LootTier.High));
        }

        private static void BuildFarm(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut, List<VehicleSpawnData> vehiclesOut)
        {
            PlaceBuilding(parent, "Ahır", BuildingStyle.Barn, center, 0f, 14f, 10f, 1, loc.Tier, rng.Next(),
                false, lootOut, structuresOut);
            PlaceBuilding(parent, "ÇobanKulübesi", BuildingStyle.ShepherdHut, OffsetGround(terrain, center, 12f, 4f), 20f,
                5f, 5f, 1, loc.Tier, rng.Next(), false, lootOut, structuresOut);
            PropFactory.Fence(parent, OffsetGround(terrain, center, -10f, -8f), 0f, rng);
            PropFactory.Fence(parent, OffsetGround(terrain, center, -10f, 8f), 0f, rng);
            PropFactory.HayBale(parent, OffsetGround(terrain, center, 6f, -6f), 10f, rng);
            PropFactory.HayBale(parent, OffsetGround(terrain, center, 8f, -5f), -15f, rng);
            vehiclesOut.Add(new VehicleSpawnData(OffsetGround(terrain, center, -14f, 0f), 90f));
        }

        private static void BuildRuins(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            for (var i = 0; i < 8; i++)
            {
                var a = (i / 8f) * Mathf.PI * 2f;
                var r = 8f + (float)rng.NextDouble() * 14f;
                PlaceBuilding(parent, "Harabe_" + i, BuildingStyle.VillageHouse,
                    OffsetGround(terrain, center, Mathf.Cos(a) * r, Mathf.Sin(a) * r), a * Mathf.Rad2Deg,
                    6f, 6f, 1, loc.Tier, rng.Next(), true, lootOut, structuresOut);
            }

            PropFactory.Wreck(parent, OffsetGround(terrain, center, 4f, -3f), 70f, rng);
            PropFactory.Rock(parent, OffsetGround(terrain, center, -5f, 5f), 0f, rng);
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, 1f, 1f), loc.Tier));
        }

        private static void BuildOutpost(
            LocationSpec loc, Vector3 center, Terrain terrain, Transform parent, System.Random rng,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            PropFactory.SandbagRing(parent, center, 0f, rng, 4f);
            PropFactory.Tent(parent, OffsetGround(terrain, center, 0f, -1f), 0f, rng);
            PropFactory.FlagPole(parent, OffsetGround(terrain, center, 3f, 3f), 0f, rng);
            PropFactory.AmmoCrate(parent, OffsetGround(terrain, center, -2f, 1f), 0f, rng);
            PlaceBuilding(parent, "Gözetleme", BuildingStyle.WatchTower, OffsetGround(terrain, center, 2f, 2f), 0f,
                3.5f, 3.5f, 2, LootTier.High, rng.Next(), false, lootOut, structuresOut);
            lootOut.Add(new LootSpawnPointData(OffsetGround(terrain, center, 0f, 0f), LootTier.High));
        }

        private static void ScatterRoadside(
            MapLayout layout, Terrain terrain, Transform parent, System.Random rng, List<LootSpawnPointData> lootOut)
        {
            if (layout.Roads == null)
                return;

            var roadside = new GameObject("[YolKenarı]");
            roadside.transform.SetParent(parent, false);

            for (var r = 0; r < layout.Roads.Count; r++)
            {
                var road = layout.Roads[r];
                if (road?.Points == null || road.Points.Count < 2)
                    continue;

                for (var i = 1; i < road.Points.Count; i += 3)
                {
                    if (rng.NextDouble() > 0.35)
                        continue;
                    var p = road.Points[i];
                    var side = rng.NextDouble() > 0.5 ? 1f : -1f;
                    var pos = OffsetGround(terrain, new Vector3(p.x, 0f, p.y), side * (road.Width * 0.5f + 3f), 0f);
                    var roll = rng.NextDouble();
                    if (roll < 0.33)
                        PropFactory.ConcreteBarrier(roadside.transform, pos, rng.Next(0, 360), rng);
                    else if (roll < 0.66)
                        PropFactory.Wreck(roadside.transform, pos, rng.Next(0, 360), rng);
                    else
                        PropFactory.Hedgehog(roadside.transform, pos, rng.Next(0, 360), rng);
                }
            }
        }

        private static void PlaceBuilding(
            Transform parent, string name, BuildingStyle style, Vector3 pos, float yaw,
            float width, float depth, int floors, LootTier tier, int seed, bool ruined,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            var spec = new BuildingSpec
            {
                Name = name,
                Style = style,
                Position = pos,
                Yaw = yaw,
                Width = width,
                Depth = depth,
                Floors = floors,
                FloorHeight = 3.2f,
                Ruined = ruined,
                Tier = tier,
                Seed = seed
            };

            BuildingResult result;
            try
            {
                result = BuildingGenerator.Build(spec, parent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LocationBuilder] Bina üretilemedi: " + name + " — " + e.Message);
                return;
            }

            if (result == null)
                return;

            if (result.Bounds.size.sqrMagnitude > 0.01f)
                structuresOut.Add(result.Bounds);

            if (result.LootPoints != null)
            {
                for (var i = 0; i < result.LootPoints.Count; i++)
                    lootOut.Add(new LootSpawnPointData(result.LootPoints[i], tier));
            }
            else
            {
                lootOut.Add(new LootSpawnPointData(pos + Vector3.up, tier));
            }
        }

        private static Vector3 GroundPoint(Terrain terrain, float x, float z)
        {
            var y = 0f;
            if (terrain != null && terrain.terrainData != null)
                y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            return new Vector3(x, y, z);
        }

        private static Vector3 OffsetGround(Terrain terrain, Vector3 center, float dx, float dz)
            => GroundPoint(terrain, center.x + dx, center.z + dz);
    }
}
