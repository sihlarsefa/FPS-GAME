#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class TreeVariantTests
    {
        [Test]
        public void PineVariants_HeightsAndHue_AreInRange()
        {
            for (var i = 0; i < TreeMeshes.PineVariantCount; i++)
            {
                var v = TreeMeshes.GetPineVariant(i);
                var h = TreeMeshes.PineRefHeight * v.HeightMul;
                Assert.GreaterOrEqual(h, 5.5f - 1e-3f);
                Assert.LessOrEqual(h, 8.5f + 1e-3f);
                Assert.LessOrEqual(System.Math.Abs(v.HueShift), 0.06f + 1e-5f);
                Assert.AreEqual(i, v.Index);
            }
            Assert.AreNotEqual(TreeMeshes.GetPineVariant(0).DensityMul, TreeMeshes.GetPineVariant(1).DensityMul);
        }

        [Test]
        public void PineSeedFor_IsDeterministic_AndSeparatesKinds()
        {
            for (var seed = -50; seed < 200; seed++)
            {
                var a = TreeMeshes.PineSeedFor(TreeKind.PineA, seed);
                var b = TreeMeshes.PineSeedFor(TreeKind.PineB, seed);
                Assert.AreEqual(a, TreeMeshes.PineSeedFor(TreeKind.PineA, seed));
                Assert.AreEqual(1, TreeMeshes.PineVariantIndex(b));
                Assert.AreNotEqual(1, TreeMeshes.PineVariantIndex(a));
            }
        }

        [Test]
        public void VariantIndex_IsAlwaysInRange_ForNegativeSeeds()
        {
            for (var seed = -20; seed < 20; seed++)
            {
                Assert.That(TreeMeshes.PineVariantIndex(seed), Is.InRange(0, 2));
                Assert.That(TreeMeshes.OakVariantIndex(seed), Is.InRange(0, 1));
            }
        }

        [Test]
        public void OakVariants_AreBrokenUp_AndDiffer()
        {
            var a = TreeMeshes.GetOakVariant(0);
            var b = TreeMeshes.GetOakVariant(1);
            Assert.Less(a.KeepChance, 1f);
            Assert.Less(b.KeepChance, a.KeepChance);
            Assert.Greater(b.DetachedChance, a.DetachedChance);
        }

        [Test]
        public void DeadTree_IsSparserThanBefore()
        {
            Assert.Less(TreeMeshes.DeadBranchCount(0), 7);
            Assert.Less(TreeMeshes.DeadBranchCount(1), 4 + 1);
        }

        [Test]
        public void NeedleTips_AreBrighter_And_Tints_AreBounded()
        {
            Assert.Greater(Project.Infrastructure.World.VegetationTextures.NeedleTipFactor(1f), Project.Infrastructure.World.VegetationTextures.NeedleTipFactor(0f));
            for (var i = 0; i < 3; i++)
            {
                var c = Project.Infrastructure.World.VegetationTextures.PineTint(i);
                Assert.That(c.r, Is.InRange(0.94f, 1.06f));
                Assert.That(c.b, Is.InRange(0.94f, 1.06f));
            }
        }
    }
}
#endif
