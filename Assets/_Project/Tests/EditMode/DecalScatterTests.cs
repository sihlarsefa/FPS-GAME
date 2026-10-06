#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    /// <summary>GX8: çıkartma dokuları (saf matematik) ve serpiştirme planı (deterministik kurallar).</summary>
    public sealed class DecalScatterTests
    {
        private static float Height(float x, float z)
        {
            return 20f + 0.8f * Mathf.Sin(x * 0.05f) * Mathf.Sin(z * 0.05f);
        }

        private static MapLayout Layout()
        {
            var l = new MapLayout { HalfSize = 300f, WaterLevel = 5f, MaxHeight = 80f, Seed = 7 };
            var dirt = new RoadSpec { Name = "toprak", Kind = RoadKind.Dirt, Width = 5f };
            var asphalt = new RoadSpec { Name = "asfalt", Kind = RoadKind.Asphalt, Width = 7f };
            for (var x = -200f; x <= 200f; x += 8f)
            {
                dirt.Points.Add(new Vector2(x, 0f));
                asphalt.Points.Add(new Vector2(x, 100f));
            }
            l.Roads.Add(dirt);
            l.Roads.Add(asphalt);
            l.Locations.Add(new LocationSpec { Name = "köy", Kind = LocationKind.Village, Center = new Vector2(0f, -100f), Radius = 60f });
            l.Locations.Add(new LocationSpec { Name = "fob", Kind = LocationKind.ForwardBase, Center = new Vector2(100f, 60f), Radius = 50f });
            l.Locations.Add(new LocationSpec { Name = "orman", Kind = LocationKind.Forest, Center = new Vector2(-120f, -120f), Radius = 60f });
            var river = new RiverSpec { Width = 8f };
            for (var z = -250f; z <= 250f; z += 10f)
                river.Points.Add(new Vector2(-250f, z));
            l.Rivers.Add(river);
            return l;
        }

        private static DecalScatterInput Input(int seed, float wet)
        {
            var buildings = new List<Bounds>();
            for (var i = 0; i < 12; i++)
                buildings.Add(new Bounds(new Vector3(-40f + i * 8f, 21f, -100f), new Vector3(6f, 4f, 6f)));
            var trees = new List<Vector3>();
            for (var i = 0; i < 60; i++)
                trees.Add(new Vector3(-170f + (i % 10) * 6f, 0f, -150f + (i / 10) * 6f));
            return new DecalScatterInput
            {
                Layout = Layout(),
                Seed = seed,
                Wet01 = wet,
                Height = Height,
                Buildings = buildings,
                Trees = trees,
                Chimneys = new List<Vector3> { new Vector3(-40f, 23f, -100f), new Vector3(-32f, 23f, -100f) }
            };
        }

        private static int Count(List<DecalPlacement> plan, DecalKind kind)
        {
            var n = 0;
            for (var i = 0; i < plan.Count; i++)
                if (plan[i].Kind == kind)
                    n++;
            return n;
        }

        // ---------------------------------------------------------------- doku matematiği

        [Test]
        public void Library_AllKinds_DeterministicAndInRange()
        {
            for (var k = 0; k < DecalLibraryMath.KindCount; k++)
            {
                var kind = (DecalKind)k;
                var a = DecalLibraryMath.Sample(kind, 0.37f, 0.61f, 5);
                var b = DecalLibraryMath.Sample(kind, 0.37f, 0.61f, 5);
                Assert.AreEqual(a.A, b.A, 0f, kind.ToString());
                Assert.AreEqual(a.R, b.R, 0f, kind.ToString());
                for (var i = 0; i < 40; i++)
                {
                    var p = DecalLibraryMath.Sample(kind, (i * 0.137f) % 1f, (i * 0.291f) % 1f, 9);
                    Assert.GreaterOrEqual(p.A, 0f, kind.ToString());
                    Assert.LessOrEqual(p.A, 1.001f, kind.ToString());
                    Assert.GreaterOrEqual(p.R, 0f, kind.ToString());
                    Assert.LessOrEqual(p.R, 1f, kind.ToString());
                    Assert.GreaterOrEqual(p.Smooth, 0f, kind.ToString());
                    Assert.LessOrEqual(p.Smooth, 1f, kind.ToString());
                }
            }
        }

        [Test]
        public void Library_AllKinds_HaveCoverage()
        {
            for (var k = 0; k < DecalLibraryMath.KindCount; k++)
            {
                var kind = (DecalKind)k;
                var sum = 0f;
                for (var y = 0; y < 24; y++)
                    for (var x = 0; x < 24; x++)
                        sum += DecalLibraryMath.Sample(kind, (x + 0.5f) / 24f, (y + 0.5f) / 24f, 1003).A;
                Assert.Greater(sum, 3f, kind + " neredeyse boş");
            }
        }

        [Test]
        public void Library_Puddle_OpaqueCenter_TransparentCorner_AndGlossy()
        {
            var c = DecalLibraryMath.Sample(DecalKind.Puddle, 0.5f, 0.5f, 3);
            var e = DecalLibraryMath.Sample(DecalKind.Puddle, 0.01f, 0.01f, 3);
            Assert.Greater(c.A, 0.8f);
            Assert.Greater(c.Smooth, 0.9f);
            Assert.Less(e.A, 0.01f);
        }

        [Test]
        public void Library_WallGrime_HeavierAtBottom_Soot_HeavierAtTop()
        {
            float grimeBottom = 0f, grimeTop = 0f, sootTop = 0f, sootBottom = 0f;
            for (var x = 0; x < 32; x++)
            {
                var u = 0.2f + 0.6f * x / 31f;
                grimeBottom += DecalLibraryMath.Sample(DecalKind.WallGrime, u, 0.05f, 4).A;
                grimeTop += DecalLibraryMath.Sample(DecalKind.WallGrime, u, 0.95f, 4).A;
                sootTop += DecalLibraryMath.Sample(DecalKind.SootStreak, u, 0.95f, 4).A;
                sootBottom += DecalLibraryMath.Sample(DecalKind.SootStreak, u, 0.05f, 4).A;
            }
            Assert.Greater(grimeBottom, grimeTop);
            Assert.Greater(sootTop, sootBottom);
        }

        [Test]
        public void Library_TireTrack_TwoBands_GapBetween()
        {
            float band = 0f, gap = 0f;
            for (var i = 0; i < 40; i++)
            {
                var v = 0.2f + 0.6f * i / 39f;
                band += DecalLibraryMath.Sample(DecalKind.TireTrack, 0.27f, v, 2).A + DecalLibraryMath.Sample(DecalKind.TireTrack, 0.73f, v, 2).A;
                gap += DecalLibraryMath.Sample(DecalKind.TireTrack, 0.5f, v, 2).A;
            }
            Assert.Greater(band, 1f);
            Assert.AreEqual(0f, gap, 0.0001f);
        }

        [Test]
        public void Library_DifferentSeed_ChangesTexture()
        {
            var diff = 0f;
            for (var i = 0; i < 30; i++)
            {
                var u = (i * 0.173f) % 1f;
                var v = (i * 0.311f) % 1f;
                diff += Mathf.Abs(DecalLibraryMath.Sample(DecalKind.Moss, u, v, 1).A - DecalLibraryMath.Sample(DecalKind.Moss, u, v, 2).A);
            }
            Assert.Greater(diff, 0.01f);
        }

        [Test]
        public void Library_BuildMaps_SizesAndMaskRule()
        {
            DecalLibraryMath.BuildMaps(DecalKind.Crack, 11, 32, out var albedo, out var normal, out var mask);
            Assert.AreEqual(32 * 32, albedo.Length);
            Assert.AreEqual(32 * 32, mask.Length);
            Assert.IsNotNull(normal);
            Assert.IsTrue(MaterialMath.IsMaskConsistent(mask));
            Assert.AreEqual(255, normal[100].a);
            DecalLibraryMath.BuildMaps(DecalKind.Puddle, 11, 16, out _, out var noNormal, out _);
            Assert.IsNull(noNormal);
        }

        // ---------------------------------------------------------------- bütçe

        [Test]
        public void Budget_Tier0_Empty_AndMonotone()
        {
            Assert.AreEqual(0, DecalScatterTiers.Get(0, 512f, 0f).Total);
            var t1 = DecalScatterTiers.Get(1, 512f, 0.5f).Total;
            var t2 = DecalScatterTiers.Get(2, 512f, 0.5f).Total;
            var t3 = DecalScatterTiers.Get(3, 512f, 0.5f).Total;
            Assert.Greater(t1, 0);
            Assert.Greater(t2, t1);
            Assert.Greater(t3, t2);
            Assert.LessOrEqual(t3, 1800);
        }

        [Test]
        public void Budget_Wet_IncreasesPuddles()
        {
            var dry = DecalScatterTiers.Get(2, 512f, 0f).PerKind[(int)DecalKind.Puddle];
            var wet = DecalScatterTiers.Get(2, 512f, 1f).PerKind[(int)DecalKind.Puddle];
            Assert.Greater(wet, dry);
        }

        [Test]
        public void Budget_DrawDistance_GrowsWithTierAndSize()
        {
            Assert.Greater(DecalScatterTiers.DrawDistance(3, 3f), DecalScatterTiers.DrawDistance(1, 3f));
            Assert.Greater(DecalScatterTiers.DrawDistance(2, 4f), DecalScatterTiers.DrawDistance(2, 1f));
        }

        // ---------------------------------------------------------------- plan

        [Test]
        public void Plan_Tier0_IsEmpty()
        {
            Assert.AreEqual(0, DecalScatterPlanner.Plan(Input(1, 0f), 0).Count);
        }

        [Test]
        public void Plan_IsDeterministic_BySeed()
        {
            var a = DecalScatterPlanner.Plan(Input(42, 0.5f), 2);
            var b = DecalScatterPlanner.Plan(Input(42, 0.5f), 2);
            var c = DecalScatterPlanner.Plan(Input(43, 0.5f), 2);
            Assert.Greater(a.Count, 50);
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Position, b[i].Position);
            }
            var different = a.Count != c.Count;
            for (var i = 0; !different && i < Mathf.Min(a.Count, c.Count); i++)
                different = a[i].Position != c[i].Position;
            Assert.IsTrue(different);
        }

        [Test]
        public void Plan_RespectsBudgetPerKind_AndGrowsWithTier()
        {
            var input = Input(5, 0.5f);
            var b2 = DecalScatterTiers.Get(2, input.Layout.HalfSize, input.Wet01);
            var p2 = DecalScatterPlanner.Plan(input, 2);
            var p1 = DecalScatterPlanner.Plan(input, 1);
            for (var k = 0; k < DecalLibraryMath.KindCount; k++)
            {
                var kind = (DecalKind)k;
                var n = Count(p2, kind);
                if (kind == DecalKind.Leaves)
                    n += Count(p2, DecalKind.Needles);
                if (kind == DecalKind.Needles)
                    continue;
                Assert.LessOrEqual(n, b2.PerKind[k], kind.ToString());
            }
            Assert.Greater(p2.Count, p1.Count);
            Assert.LessOrEqual(p2.Count, b2.Total);
        }

        [Test]
        public void Plan_Wet_MorePuddles()
        {
            var dry = Count(DecalScatterPlanner.Plan(Input(9, 0f), 3), DecalKind.Puddle);
            var wet = Count(DecalScatterPlanner.Plan(Input(9, 1f), 3), DecalKind.Puddle);
            Assert.Greater(wet, dry);
        }

        [Test]
        public void Plan_GroundDecals_AvoidBuildingsWaterAndRiver()
        {
            var input = Input(11, 1f);
            var plan = DecalScatterPlanner.Plan(input, 3);
            foreach (var p in plan)
            {
                if (p.Surface != DecalSurface.Ground)
                    continue;
                Assert.GreaterOrEqual(p.Position.y, input.Layout.WaterLevel);
                Assert.IsTrue(DecalScatterPlanner.GroundFree(input, p.Position.x, p.Position.z, 0f, 0f), p.Kind.ToString());
            }
        }

        [Test]
        public void Plan_TireTracks_AlignWithRoad()
        {
            var plan = DecalScatterPlanner.Plan(Input(3, 0f), 3);
            var found = 0;
            foreach (var p in plan)
            {
                if (p.Kind != DecalKind.TireTrack || p.Size.x < 1.5f) // küçük olanlar kapı önü süpürge izi
                    continue;
                found++;
                // Yollar X ekseninde: yaw 90 veya 270 (±), konum yol şeridinde.
                var yaw = Mathf.Repeat(p.Yaw, 180f);
                Assert.AreEqual(90f, yaw, 0.01f);
                var dz = Mathf.Min(Mathf.Abs(p.Position.z - 0f), Mathf.Abs(p.Position.z - 100f));
                Assert.Less(dz, 4f);
            }
            Assert.Greater(found, 5);
        }

        [Test]
        public void Plan_Leaves_AreNearTrees()
        {
            var input = Input(8, 0f);
            var plan = DecalScatterPlanner.Plan(input, 3);
            var n = 0;
            foreach (var p in plan)
            {
                if (p.Kind != DecalKind.Leaves && p.Kind != DecalKind.Needles)
                    continue;
                n++;
                var best = float.MaxValue;
                foreach (var t in input.Trees)
                    best = Mathf.Min(best, Vector2.Distance(new Vector2(t.x, t.z), new Vector2(p.Position.x, p.Position.z)));
                Assert.LessOrEqual(best, 2.6f);
            }
            Assert.Greater(n, 10);
        }

        [Test]
        public void Plan_WallDecals_FaceInward_AndStayNearBuildings()
        {
            var input = Input(6, 0f);
            var plan = DecalScatterPlanner.Plan(input, 3);
            var walls = 0;
            foreach (var p in plan)
            {
                if (p.Surface != DecalSurface.Wall)
                    continue;
                walls++;
                Assert.AreEqual(1f, p.Dir.magnitude, 0.001f);
                var near = false;
                foreach (var b in input.Buildings)
                {
                    if (Mathf.Abs(p.Position.x - b.center.x) <= b.extents.x + 2.6f && Mathf.Abs(p.Position.z - b.center.z) <= b.extents.z + 2.6f)
                        near = true;
                }
                Assert.IsTrue(near, p.Kind.ToString());
            }
            Assert.Greater(walls, 10);
        }

        [Test]
        public void Plan_Soot_UsesChimneysAsRoofDecals()
        {
            var plan = DecalScatterPlanner.Plan(Input(2, 0f), 2);
            var roofs = 0;
            foreach (var p in plan)
                if (p.Surface == DecalSurface.Roof)
                {
                    roofs++;
                    Assert.AreEqual(DecalKind.SootStreak, p.Kind);
                }
            Assert.AreEqual(2, roofs);
        }

        [Test]
        public void Plan_Rank_IsNormalizedPerKind()
        {
            var plan = DecalScatterPlanner.Plan(Input(4, 0.5f), 3);
            foreach (var p in plan)
            {
                Assert.GreaterOrEqual(p.Rank01, 0f);
                Assert.Less(p.Rank01, 1f);
            }
        }

        [Test]
        public void ActiveFraction_DropsWithLowerTier()
        {
            Assert.AreEqual(1f, DecalScatterTiers.ActiveFraction(3, 3, 512f, 0f), 0.0001f);
            Assert.Less(DecalScatterTiers.ActiveFraction(3, 1, 512f, 0f), 0.5f);
            Assert.AreEqual(0f, DecalScatterTiers.ActiveFraction(3, 0, 512f, 0f), 0.0001f);
        }

        // ---------------------------------------------------------------- G3 köy dekal zenginliği

        [Test]
        public void VillageDensity_RisesTowardCenter_AndFlatWithoutVillage()
        {
            var l = Layout();
            var c = DecalScatterPlanner.VillageDensity(l, new Vector2(0f, -100f));
            var mid = DecalScatterPlanner.VillageDensity(l, new Vector2(40f, -100f));
            var far = DecalScatterPlanner.VillageDensity(l, new Vector2(250f, 250f));
            Assert.Greater(c, mid);
            Assert.Greater(mid, far);
            Assert.AreEqual(1f, c, 0.001f);
            Assert.AreEqual(0.25f, far, 0.001f);
            l.Locations.RemoveAll(x => x.Kind == LocationKind.Village);
            Assert.AreEqual(0.6f, DecalScatterPlanner.VillageDensity(l, Vector2.zero), 0.001f);
        }

        [Test]
        public void Plan_Village_HasDoorWearSweepThresholdAndRoadDrops_Deterministic()
        {
            var a = DecalScatterPlanner.Plan(Input(11, 0.2f), 3);
            var b = DecalScatterPlanner.Plan(Input(11, 0.2f), 3);
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i].Position, b[i].Position);
            // Bina dibi zemin dekalleri (kapı önü/eşik/süpürge) ve küçük yol kenarı yağ damlaları.
            var foot = 0;
            var drops = 0;
            var eave = 0;
            for (var i = 0; i < a.Count; i++)
            {
                var p = a[i];
                if (p.Kind == DecalKind.OilStain && p.Size.x < 0.75f)
                    drops++;
                if (p.Kind == DecalKind.SootStreak && p.Surface == DecalSurface.Wall && p.Size.x < 0.65f)
                    eave++;
                if (p.Surface == DecalSurface.Ground && (p.Kind == DecalKind.MudSplat || p.Kind == DecalKind.Crack || p.Kind == DecalKind.TireTrack)
                    && p.Size.x < 2.3f && Mathf.Abs(p.Position.z + 100f) < 6f)
                    foot++;
            }
            Assert.Greater(foot, 0);
            Assert.Greater(drops, 0);
            Assert.Greater(eave, 0);
        }

        [Test]
        public void Plan_WallDecals_DenserNearVillageCenter()
        {
            var input = Input(3, 0f);
            var plan = DecalScatterPlanner.Plan(input, 3);
            int center = 0, edge = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                if (plan[i].Surface != DecalSurface.Wall)
                    continue;
                var d = Mathf.Abs(plan[i].Position.x);
                if (d < 20f) center++;
                else if (d > 28f) edge++;
            }
            // Ortadaki bina sayısı kenardakinden az (5 vs 6) olsa da merkezde kişi başı daha çok dekal olmalı.
            Assert.Greater(center / 5f, edge / 7f);
        }

        [Test]
        public void Plan_TierLimits_StillHold_WithRichVillage()
        {
            var input = Input(8, 0.5f);
            for (var t = 1; t <= 3; t++)
            {
                var b = DecalScatterTiers.Get(t, input.Layout.HalfSize, input.Wet01);
                var plan = DecalScatterPlanner.Plan(input, t);
                Assert.LessOrEqual(plan.Count, b.Total, "tier " + t);
            }
            Assert.AreEqual(0, DecalScatterPlanner.Plan(input, 0).Count);
        }
    }
}
#endif
