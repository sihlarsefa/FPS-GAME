using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>300×300 m Atış Poligonu sahnesi üreticisi.</summary>
    public static class TrainingRangeBuilder
    {
        public const float HalfSize = 150f;

        public static WorldMetadata Build(Transform parent)
        {
            var root = new GameObject("[Atış Poligonu]");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var rng = new System.Random(1923);
            BuildGround(root.transform);
            BuildLanes(root.transform, rng);
            BuildKillHouse(root.transform, rng);
            BuildExtras(root.transform, rng);

            var meta = root.AddComponent<WorldMetadata>();
            meta.MapHalfSize = HalfSize;
            meta.WaterLevel = -10f;
            meta.MaxHeight = 20f;
            meta.MapCenter = Vector2.zero;
            meta.Locations = new List<NamedLocation>
            {
                new NamedLocation
                {
                    Name = "Atış Poligonu",
                    Center = Vector2.zero,
                    Radius = HalfSize,
                    IsMajor = true
                }
            };
            meta.GroundSpawnPoints = new List<Vector3> { new Vector3(0f, 0.1f, -120f) };
            meta.LandingZones = new List<Vector3> { new Vector3(0f, 0.1f, -100f) };
            meta.LootPoints = new List<LootSpawnPointData>();
            meta.VehicleSpawns = new List<VehicleSpawnData>();
            meta.StructureBounds = new List<Bounds>();

            // Silah rafı yağma noktaları
            for (var i = 0; i < 12; i++)
            {
                var x = -20f + i * 3.5f;
                meta.LootPoints.Add(new LootSpawnPointData(new Vector3(x, 1f, -110f), LootTier.Military));
            }

            meta.LootPoints.Add(new LootSpawnPointData(new Vector3(-25f, 1f, 10f), LootTier.High));
            meta.LootPoints.Add(new LootSpawnPointData(new Vector3(25f, 1f, 10f), LootTier.High));

            return meta;
        }

        private static void BuildGround(Transform parent)
        {
            var ground = StructureKit.CreateBox(parent, "Zemin", new Vector3(0f, -0.05f, 0f),
                new Vector3(HalfSize * 2f, 0.1f, HalfSize * 2f), Quaternion.identity, MaterialId.Dirt);
            StructureKit.MarkStatic(ground);

            // Çit çevre
            var edge = HalfSize - 2f;
            for (var i = -3; i <= 3; i++)
            {
                PropFactory.Fence(parent, new Vector3(i * 40f, 0f, edge), 0f, null);
                PropFactory.Fence(parent, new Vector3(i * 40f, 0f, -edge), 0f, null);
                PropFactory.Fence(parent, new Vector3(edge, 0f, i * 40f), 90f, null);
                PropFactory.Fence(parent, new Vector3(-edge, 0f, i * 40f), 90f, null);
            }
        }

        private static void BuildLanes(Transform parent, System.Random rng)
        {
            var lanes = new GameObject("AtışŞeritleri");
            lanes.transform.SetParent(parent, false);

            var distances = new[] { 25, 50, 100, 200, 300 };
            for (var lane = 0; lane < 6; lane++)
            {
                var x = -40f + lane * 16f;
                StructureKit.CreateBox(lanes.transform, "Şerit" + lane, new Vector3(x, 0.02f, 40f),
                    new Vector3(2.2f, 0.04f, 260f), Quaternion.identity, MaterialId.Asphalt, false);

                // Ateş hattı
                StructureKit.CreateBox(lanes.transform, "Hat" + lane, new Vector3(x, 0.5f, -110f),
                    new Vector3(2.4f, 1f, 0.3f), Quaternion.identity, MaterialId.Concrete);

                for (var d = 0; d < distances.Length; d++)
                {
                    var z = -110f + distances[d];
                    if (z > HalfSize - 5f)
                        continue;

                    // Mesafe levhası
                    var board = StructureKit.CreateBox(lanes.transform, distances[d] + "m",
                        new Vector3(x - 1.4f, 1.2f, z), new Vector3(0.08f, 1.6f, 0.6f),
                        Quaternion.identity, MaterialId.Wood);
                    StructureKit.CreateBox(lanes.transform, "Levhа" + distances[d],
                        new Vector3(x - 1.35f, 1.5f, z), new Vector3(0.05f, 0.5f, 0.7f),
                        Quaternion.identity, MaterialId.White, false);

                    // Hedef standı
                    StructureKit.CreateBox(lanes.transform, "Hedef" + lane + "_" + d,
                        new Vector3(x, 1.1f, z), new Vector3(0.6f, 1.8f, 0.15f),
                        Quaternion.identity, MaterialId.Wood);
                }
            }

            // Silah rafları
            for (var i = 0; i < 12; i++)
            {
                var x = -20f + i * 3.5f;
                StructureKit.CreateBox(lanes.transform, "Raf" + i, new Vector3(x, 0.7f, -112f),
                    new Vector3(0.8f, 1.3f, 0.4f), Quaternion.identity, MaterialId.MetalDark);
                PropFactory.AmmoCrate(lanes.transform, new Vector3(x, 0f, -114f), 0f, rng);
            }
        }

        private static void BuildKillHouse(Transform parent, System.Random rng)
        {
            var house = new GameObject("KillHouse");
            house.transform.SetParent(parent, false);

            var a = BuildingGenerator.Build(new BuildingSpec
            {
                Name = "KillHouse_A",
                Style = BuildingStyle.Warehouse,
                Position = new Vector3(-30f, 0f, 20f),
                Yaw = 0f,
                Width = 14f,
                Depth = 10f,
                Floors = 1,
                Tier = LootTier.High,
                Seed = rng.Next()
            }, house.transform);

            var b = BuildingGenerator.Build(new BuildingSpec
            {
                Name = "KillHouse_B",
                Style = BuildingStyle.TwoStoryHouse,
                Position = new Vector3(30f, 0f, 18f),
                Yaw = 15f,
                Width = 10f,
                Depth = 8f,
                Floors = 2,
                Tier = LootTier.High,
                Seed = rng.Next()
            }, house.transform);

            // Engeller
            for (var i = 0; i < 6; i++)
            {
                PropFactory.ConcreteBarrier(house.transform, new Vector3(-8f + i * 4f, 0f, 5f), 0f, rng);
                PropFactory.Sandbags(house.transform, new Vector3(-6f + i * 3f, 0f, 35f), i * 15f, rng);
            }

            PropFactory.Hesco(house.transform, new Vector3(0f, 0f, 45f), 0f, rng);
        }

        private static void BuildExtras(Transform parent, System.Random rng)
        {
            PropFactory.FlagPole(parent, new Vector3(-20f, 0f, -120f), 0f, rng);
            PropFactory.FlagPole(parent, new Vector3(20f, 0f, -120f), 0f, rng);
            PropFactory.FlagPole(parent, new Vector3(0f, 0f, HalfSize - 10f), 0f, rng);
            PropFactory.StreetLamp(parent, new Vector3(-50f, 0f, -100f), 0f, rng);
            PropFactory.StreetLamp(parent, new Vector3(50f, 0f, -100f), 0f, rng);
            PropFactory.GuardBooth(parent, new Vector3(0f, 0f, -HalfSize + 8f), 0f, rng);
            PropFactory.Helipad(parent, new Vector3(80f, 0f, -80f), 0f, rng);
            PropFactory.Tent(parent, new Vector3(-70f, 0f, -90f), 20f, rng);
            PropFactory.Container(parent, new Vector3(70f, 0f, -95f), 90f, rng);
        }
    }
}
