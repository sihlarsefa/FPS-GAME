#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class VegetationVarietyTests
    {
        [Test]
        public void Specs_AreSane()
        {
            for (var i = 0; i < VegetationVariety.KindCount; i++)
            {
                var s = VegetationVariety.Spec((VarietyKind)i);
                Assert.Greater(s.Height, 0.5f);
                Assert.Greater(s.CrownRadius, 0.1f);
            }
        }

        [Test]
        public void DwarfOak_IsShorterThanPoplar()
        {
            Assert.Greater(VegetationVariety.Spec(VarietyKind.Poplar).Height, VegetationVariety.Spec(VarietyKind.DwarfOak).Height);
        }

        [Test]
        public void Wind_OffAtTierZero_FullAtHigh()
        {
            var off = VegetationVariety.WindFor(VarietyKind.Poplar, 0);
            Assert.AreEqual(0f, off.Bend, 1e-5f);
            var hi = VegetationVariety.WindFor(VarietyKind.Poplar, 3);
            Assert.Greater(hi.Bend, VegetationVariety.WindFor(VarietyKind.Poplar, 1).Bend);
        }

        [Test]
        public void BendWeight_GrowsWithHeight()
        {
            Assert.AreEqual(0f, VegetationVariety.BendWeight(0f), 1e-5f);
            Assert.Greater(VegetationVariety.BendWeight(0.9f), VegetationVariety.BendWeight(0.4f));
            Assert.AreEqual(1f, VegetationVariety.BendWeight(2f), 1e-5f);
        }

        [Test]
        public void MossCoverage_WetShadyFlatBeatsDrySteep()
        {
            Assert.Greater(VegetationVariety.MossCoverage(0.9f, 0.8f, 5f), VegetationVariety.MossCoverage(0.2f, 0.1f, 5f));
            Assert.AreEqual(0f, VegetationVariety.MossCoverage(1f, 1f, 70f), 1e-5f);
        }

        [Test]
        public void BarkBump_ScalesWithTier()
        {
            Assert.AreEqual(0f, VegetationVariety.BarkBumpScale(VarietyKind.DwarfOak, 0), 1e-5f);
            Assert.Greater(VegetationVariety.BarkBumpScale(VarietyKind.DwarfOak, 2), VegetationVariety.BarkBumpScale(VarietyKind.DwarfOak, 1));
        }

        [Test]
        public void BarkNormalPixels_AreUnitZUp()
        {
            var px = VegetationVariety.BarkNormalPixels(16, 3, 2.5f);
            Assert.AreEqual(256, px.Length);
            Assert.Greater(px[40].b, 100);
        }
    }
}
#endif
