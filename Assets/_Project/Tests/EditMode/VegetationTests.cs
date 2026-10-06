#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class VegetationTests
    {
        private static float[] Weights(float grass = 0f, float dry = 0f, float rock = 0f, float snow = 0f, float asphalt = 0f)
        {
            var w = new float[TerrainTextureFactory.LayerCount];
            w[(int)TerrainLayerKind.Grass] = grass;
            w[(int)TerrainLayerKind.DryGrass] = dry;
            w[(int)TerrainLayerKind.Rock] = rock;
            w[(int)TerrainLayerKind.Snow] = snow;
            w[(int)TerrainLayerKind.Asphalt] = asphalt;
            return w;
        }

        [Test]
        public void Grass_GrowsOnMeadow_NotOnRoadRockSnowOrSteep()
        {
            var d = new float[VegetationPainter.KindCount];
            VegetationPainter.ComputeDensities(Weights(grass: 1f), 5f, 20f, 0.8f, 0.1f, d);
            Assert.Greater(d[(int)DetailKind.Grass], 0.5f);

            VegetationPainter.ComputeDensities(Weights(asphalt: 1f), 2f, 20f, 0.9f, 0.9f, d);
            Assert.AreEqual(0f, Sum(d), 1e-4f, "yolda bitki yok");
            VegetationPainter.ComputeDensities(Weights(rock: 1f), 20f, 20f, 0.9f, 0.9f, d);
            Assert.AreEqual(0f, Sum(d), 1e-4f, "kayada yok");
            VegetationPainter.ComputeDensities(Weights(snow: 1f), 5f, 20f, 0.9f, 0.9f, d);
            Assert.AreEqual(0f, Sum(d), 1e-4f, "karda yok");
            VegetationPainter.ComputeDensities(Weights(grass: 1f), 50f, 20f, 0.9f, 0.9f, d);
            Assert.AreEqual(0f, Sum(d), 1e-4f, "dik yamaçta yok");
            VegetationPainter.ComputeDensities(Weights(grass: 1f), 5f, -2f, 0.9f, 0.9f, d);
            Assert.AreEqual(0f, Sum(d), 1e-4f, "suyun altında yok");
        }

        [Test]
        public void Flowers_OnlyInClustersOnMeadow()
        {
            var d = new float[VegetationPainter.KindCount];
            VegetationPainter.ComputeDensities(Weights(grass: 1f), 3f, 20f, 0.7f, 0.2f, d);
            Assert.AreEqual(0f, d[2] + d[3] + d[4], 1e-4f);
            VegetationPainter.ComputeDensities(Weights(grass: 1f), 3f, 20f, 0.7f, 0.95f, d);
            Assert.Greater(d[2] + d[3] + d[4], 0.2f);
        }

        [Test]
        public void DryGrass_PrefersDryLayer()
        {
            var d = new float[VegetationPainter.KindCount];
            VegetationPainter.ComputeDensities(Weights(dry: 1f), 5f, 20f, 0.9f, 0.1f, d);
            Assert.Greater(d[(int)DetailKind.DryGrass], d[(int)DetailKind.Grass]);
        }

        [Test]
        public void ToCount_RespectsMax()
        {
            Assert.AreEqual(0, VegetationPainter.ToCount(DetailKind.Grass, 0f));
            Assert.AreEqual(VegetationPainter.MaxPerCell[0], VegetationPainter.ToCount(DetailKind.Grass, 5f));
        }

        [Test]
        public void DetailDensity_ScalesWithQualityTier()
        {
            Assert.AreEqual(0f, PerformanceProfile.DetailDensityScale(0));
            Assert.Less(PerformanceProfile.DetailDensityScale(1), PerformanceProfile.DetailDensityScale(2));
            Assert.AreEqual(1f, PerformanceProfile.DetailDensityScale(3));
            Assert.AreEqual(1f, PerformanceProfile.DetailDensityScale(99));
        }

        [Test]
        public void TreeTargetCount_GrowsWithTier()
        {
            Assert.Less(TreeScatter.TargetCountForTier(0), TreeScatter.TargetCountForTier(1));
            Assert.Less(TreeScatter.TargetCountForTier(2), TreeScatter.TargetCountForTier(3));
            Assert.AreEqual(TreeScatter.DefaultTargetCount, TreeScatter.TargetCountForTier(3));
        }

        [Test]
        public void Textures_HaveReasonableCoverage_AndAreDeterministic()
        {
            var needles = VegetationTextures.NeedleCard(64, 7);
            var leaves = VegetationTextures.LeafCluster(64, 7);
            var grass = VegetationTextures.GrassTuft(64, 7);
            var flower = VegetationTextures.Flower(64, 7);
            foreach (var px in new[] { needles, leaves, grass, flower })
            {
                var c = VegetationTextures.Coverage(px);
                Assert.Greater(c, 0.04f);
                Assert.Less(c, 0.85f);
            }

            var again = VegetationTextures.NeedleCard(64, 7);
            for (var i = 0; i < needles.Length; i++)
                Assert.AreEqual(needles[i], again[i]);
        }

        [Test]
        public void MossFaces_AreOnTopOnly()
        {
            Assert.IsTrue(RockFactory.IsMossFace(0.95f, 0.3f, 0.5f));
            Assert.IsFalse(RockFactory.IsMossFace(0.2f, 0.3f, 0.0f), "dik yüzde yosun yok");
            Assert.IsFalse(RockFactory.IsMossFace(-0.9f, 0.3f, 0.0f), "alt yüzde yosun yok");
            Assert.IsFalse(RockFactory.IsMossFace(0.95f, -0.3f, 0.0f), "tabanda yosun yok");
        }

        [Test]
        public void TreeLods_AreWithinTriangleBudget_AndHaveTwoSubmeshes()
        {
            for (var k = 0; k < TreeFactory.KindCount; k++)
            {
                var kind = (TreeKind)k;
                var h = TreeFactory.BaseHeight(kind);
                for (var lod = 0; lod <= TreeMeshes.MaxLod; lod++)
                {
                    Mesh m;
                    switch (kind)
                    {
                        case TreeKind.PineA:
                        case TreeKind.PineB: m = MeshFactory.PineTreeLod(h, 5, lod); break;
                        case TreeKind.Oak: m = MeshFactory.OakTreeLod(h, 5, lod); break;
                        case TreeKind.Dead: m = MeshFactory.DeadTreeLod(h, 5, lod); break;
                        default: m = MeshFactory.BushLod(h, 5, lod); break;
                    }

                    Assert.AreEqual(2, m.subMeshCount);
                    Assert.LessOrEqual(m.triangles.Length / 3, TreeMeshes.TriangleBudget(kind, lod), kind + " LOD" + lod);
                    Assert.Greater(m.triangles.Length, 0);
                }

                Assert.Greater(MeshFactory.PineTreeLod(11f, 5, 0).triangles.Length, MeshFactory.PineTreeLod(11f, 5, 1).triangles.Length);
            }
        }

        [Test]
        public void SplitRock_HasMossSubmesh_AndMoreFacets()
        {
            var m = RockFactory.GetSplitVariant(0);
            Assert.AreEqual(2, m.subMeshCount);
            Assert.Greater(m.GetTriangles(1).Length, 0, "yosun yüzleri var");
            Assert.Greater(m.GetTriangles(0).Length, 0);
            Assert.GreaterOrEqual(m.triangles.Length / 3, 300);
        }

        [Test]
        public void CellRange_ClampsAndRejectsOutside()
        {
            var b = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(10f, 5f, 10f));
            Assert.IsTrue(VegetationPainter.CellRange(b, 0f, new Vector3(-500f, 0f, -500f), new Vector3(1000f, 100f, 1000f), 512, out var x0, out var x1, out var z0, out var z1));
            Assert.LessOrEqual(x0, 256);
            Assert.GreaterOrEqual(x1, 256);
            Assert.LessOrEqual(z0, z1);
            var far = new Bounds(new Vector3(5000f, 0f, 0f), Vector3.one);
            Assert.IsFalse(VegetationPainter.CellRange(far, 0f, new Vector3(-500f, 0f, -500f), new Vector3(1000f, 100f, 1000f), 512, out _, out _, out _, out _));
        }

        private static float Sum(float[] a)
        {
            var s = 0f;
            for (var i = 0; i < a.Length; i++)
                s += a[i];
            return s;
        }
    
        [Test]
        public void VegetationTuning_SpeciesRockClassAndWind()
        {
            Assert.AreEqual("pine", VegetationTuning.SpeciesId(TreeKind.PineB));
            Assert.AreEqual("bush", VegetationTuning.SpeciesId(TreeKind.Bush));
            Assert.AreEqual("small", VegetationTuning.RockSizeClass(0.7f));
            Assert.AreEqual("medium", VegetationTuning.RockSizeClass(1.3f));
            Assert.AreEqual("large", VegetationTuning.RockSizeClass(2.5f));
            Assert.AreEqual(0f, VegetationTuning.WindFor(TreeKind.Dead, 3).Bend, 1e-5f);
            Assert.AreEqual(0f, VegetationTuning.WindFor(TreeKind.Oak, 0).Amount, 1e-5f);
            Assert.Greater(VegetationTuning.WindFor(TreeKind.Bush, 2).Bend, VegetationTuning.WindFor(TreeKind.PineA, 2).Bend);
            Assert.GreaterOrEqual(VegetationTuning.BillboardDistance(0), 60f);
        }
}
}
#endif
