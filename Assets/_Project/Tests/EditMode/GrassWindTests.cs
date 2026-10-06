using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class GrassWindTests
    {
        [Test]
        public void Tiers_LowIsOffAndCostGrows()
        {
            Assert.IsFalse(GrassRules.ForTier(0).Enabled);
            var t1 = GrassRules.ForTier(1);
            var t2 = GrassRules.ForTier(2);
            var t3 = GrassRules.ForTier(3);
            Assert.IsTrue(t1.Enabled);
            Assert.Less(t1.DrawDistance, t2.DrawDistance);
            Assert.Less(t2.DrawDistance, t3.DrawDistance);
            Assert.Less(t1.DensityPerSqm, t3.DensityPerSqm);
            Assert.Less(GrassRules.CandidatesPerCell(t1), GrassRules.CandidatesPerCell(t3));
            Assert.AreEqual(0, GrassRules.CandidatesPerCell(GrassRules.ForTier(0)));
        }

        [Test]
        public void Density_GrassYesRoadRockSnowNo()
        {
            var w = new float[8];
            w[0] = 1f;
            Assert.Greater(GrassRules.Density(w, 8), 0.95f);
            w[0] = 0f; w[7] = 1f;
            Assert.AreEqual(0f, GrassRules.Density(w, 8), 0.0001f, "asfalt");
            w[7] = 0f; w[3] = 1f;
            Assert.AreEqual(0f, GrassRules.Density(w, 8), 0.0001f, "kaya");
            w[3] = 0f; w[6] = 1f;
            Assert.AreEqual(0f, GrassRules.Density(w, 8), 0.0001f, "kar");
            w[6] = 0f; w[0] = 0.5f; w[3] = 0.5f;
            Assert.AreEqual(0.5f, GrassRules.Density(w, 8), 0.001f);
            w[0] = 0.5f; w[1] = 0.5f; w[3] = 0f;
            Assert.AreEqual(0.5f, GrassRules.Dryness(w, 8), 0.001f);
        }

        [Test]
        public void Hash_IsDeterministicAndInRange()
        {
            for (var i = 0; i < 200; i++)
            {
                float a = GrassRules.Hash01(3, -4, i, 9);
                Assert.AreEqual(a, GrassRules.Hash01(3, -4, i, 9), 0f);
                Assert.IsTrue(a >= 0f && a < 1f);
            }

            Assert.AreNotEqual(GrassRules.Hash01(3, -4, 1, 9), GrassRules.Hash01(4, -4, 1, 9));
        }

        [Test]
        public void KeepFractionAndDistanceScale_Monotonic()
        {
            Assert.AreEqual(1f, GrassRules.KeepFraction(5f, 60f), 0f);
            Assert.Less(GrassRules.KeepFraction(55f, 60f), GrassRules.KeepFraction(35f, 60f));
            Assert.GreaterOrEqual(GrassRules.KeepFraction(500f, 60f), 0.25f);
            Assert.AreEqual(1f, GrassRules.DistanceScale(10f, 60f), 0f);
            Assert.AreEqual(0f, GrassRules.DistanceScale(60f, 60f), 0f);
            Assert.AreEqual(0.5f, GrassRules.DistanceScale(55f, 60f), 0.001f);
        }

        [Test]
        public void CellMath_RangeAndIndex()
        {
            Assert.AreEqual(0, GrassRules.CellIndex(15.9f));
            Assert.AreEqual(1, GrassRules.CellIndex(16f));
            Assert.AreEqual(-1, GrassRules.CellIndex(-0.1f));
            Assert.IsTrue(GrassRules.CellInRange(0, 0, 8f, 8f, 40f));
            Assert.IsFalse(GrassRules.CellInRange(10, 10, 0f, 0f, 40f));
            Assert.IsTrue(GrassRules.CellShouldKeep(4, 0, 0f, 0f, 40f));
        }

        [Test]
        public void Trample_FallsOffWithDistance()
        {
            Assert.AreEqual(1f, GrassRules.TrampleFactor(0f, 2f), 0.0001f);
            Assert.AreEqual(0f, GrassRules.TrampleFactor(2f, 2f), 0.0001f);
            Assert.Less(GrassRules.TrampleFactor(1.5f, 2f), GrassRules.TrampleFactor(0.5f, 2f));
            Assert.AreEqual(0f, GrassRules.TrampleFactor(0.1f, 0f), 0f);
        }

        [Test]
        public void PickNearest_SortedAndCapped()
        {
            var d = new[] { 25f, 4f, 100f, 1f, 9f };
            var idx = new int[3];
            int n = GrassRules.PickNearest(d, d.Length, 3, 1000f, idx);
            Assert.AreEqual(3, n);
            Assert.AreEqual(3, idx[0]);
            Assert.AreEqual(1, idx[1]);
            Assert.AreEqual(4, idx[2]);
            n = GrassRules.PickNearest(d, d.Length, 3, 10f, idx);
            Assert.AreEqual(3, n);
            n = GrassRules.PickNearest(d, d.Length, 3, 0.5f, idx);
            Assert.AreEqual(0, n);
        }

        [Test]
        public void DensityMap_SamplesBilinearAndOutsideIsZero()
        {
            var m = new GrassDensityMap(2, 2, 0f, 0f, 10f, 10f);
            m.Set(0, 0, 0f, 0f); m.Set(1, 0, 1f, 1f); m.Set(0, 1, 0f, 0f); m.Set(1, 1, 1f, 1f);
            Assert.AreEqual(0.5f, m.SampleDensity(5f, 5f), 0.01f);
            Assert.AreEqual(1f, m.SampleDensity(10f, 3f), 0.01f);
            Assert.AreEqual(0f, m.SampleDensity(-1f, 3f), 0f);
            Assert.AreEqual(0.5f, m.SampleDryness(5f, 0f), 0.01f);
        }

        [Test]
        public void BladeGeometry_IsConsistentForAllVariants()
        {
            for (var v = 0; v < GrassRules.VariantCount; v++)
            {
                var g = GrassBladeGeometry.Build(v, 77);
                int vc = g.VertexCount;
                Assert.Greater(vc, 10);
                Assert.AreEqual(vc * 3, g.Normals.Length);
                Assert.AreEqual(vc * 2, g.Uvs.Length);
                Assert.AreEqual(vc * 4, g.Colors.Length);
                Assert.AreEqual(0, g.Indices.Length % 3);
                for (var i = 0; i < g.Indices.Length; i++)
                    Assert.IsTrue(g.Indices[i] >= 0 && g.Indices[i] < vc);
                for (var i = 0; i < vc; i++)
                {
                    float bend = g.Colors[i * 4];
                    Assert.IsTrue(bend >= 0f && bend <= 1.0001f, "egilme agirligi 0..1");
                }

                Assert.Greater(g.MaxHeight, 0.3f);
            }

            Assert.Less(GrassBladeGeometry.Build(1, 77, 0.5f).VertexCount, GrassBladeGeometry.Build(1, 77, 1f).VertexCount);
            Assert.Greater(GrassBladeGeometry.Build(2, 77).MaxHeight, GrassBladeGeometry.Build(0, 77).MaxHeight);
        }

        [Test]
        public void Wind_WeatherAndTier()
        {
            var clear = WindRules.ForWeather(0, 2, 0f);
            var rain = WindRules.ForWeather(1, 2, 0f);
            Assert.Greater(rain.Strength, clear.Strength);
            Assert.IsTrue(clear.Enabled);
            var off = WindRules.ForWeather(1, 0, 0f);
            Assert.IsFalse(off.Enabled);
            Assert.AreEqual(0f, off.Strength, 0f);
            Assert.AreEqual(0f, WindRules.ForWeather(1, 1, 0f).Turbulence, 0f);
            Assert.Greater(WindRules.ForWeather(1, 3, 0f).Turbulence, 0f);
            WindRules.DirFromAngle(90f, out var x, out var z);
            Assert.AreEqual(1f, x, 0.001f);
            Assert.AreEqual(0f, z, 0.001f);
        }

        [Test]
        public void Wind_ApproachConverges()
        {
            var cur = WindRules.ForWeather(0, 2, 0f);
            var tgt = WindRules.ForWeather(1, 2, 0f);
            float before = cur.Strength;
            cur = WindRules.Approach(cur, tgt, 0.6f, 0.5f);
            Assert.Greater(cur.Strength, before);
            Assert.Less(cur.Strength, tgt.Strength);
            for (var i = 0; i < 100; i++) cur = WindRules.Approach(cur, tgt, 0.6f, 0.5f);
            Assert.AreEqual(tgt.Strength, cur.Strength, 0.001f);
            Assert.AreEqual(65f, WindRules.DriftAngle(65f, 0f), 0.0001f);
        }
    }
}
