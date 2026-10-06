#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class VegetationScatterTests
    {
        [Test]
        public void Poplar_NearWater()
        {
            var w = TreeScatter.VarietyWeight(8f, 5f, 2f, 5f, 0f, 0.5f, 0.1f, out var kind);
            Assert.Greater(w, 0f);
            Assert.IsTrue(kind == VarietyKind.Poplar);
        }

        [Test]
        public void DwarfOak_DrySlope()
        {
            var w = TreeScatter.VarietyWeight(200f, 5f, 30f, 25f, 0f, 0.9f, 0.1f, out var kind);
            Assert.Greater(w, 0f);
            Assert.IsTrue(kind == VarietyKind.DwarfOak);
        }

        [Test]
        public void Shrub_AtForestEdge_AndNothingOnFlatDry()
        {
            var w = TreeScatter.VarietyWeight(200f, 5f, 30f, 5f, 0.3f, 0.5f, 0.1f, out var kind);
            Assert.Greater(w, 0f);
            Assert.IsTrue(kind == VarietyKind.ShrubRound || kind == VarietyKind.ShrubSparse);
            Assert.AreEqual(0f, TreeScatter.VarietyWeight(200f, 5f, 30f, 3f, 0f, 0.9f, 0.1f, out kind), 1e-5f);
        }

        [Test]
        public void CountGrowsWithTier()
        {
            Assert.Greater(TreeScatter.VarietyCountForTier(3), TreeScatter.VarietyCountForTier(0));
            Assert.AreEqual("poplar", VegetationVariety.SpeciesId(VarietyKind.Poplar));
        }
    }
}
#endif
