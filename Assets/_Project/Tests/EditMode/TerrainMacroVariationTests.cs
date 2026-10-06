using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class TerrainMacroVariationTests
    {
        [Test]
        public void Evaluate_BoundedAndFinite()
        {
            for (var s = -1f; s <= 1f; s += 0.25f)
            for (var m = -1f; m <= 1f; m += 0.25f)
            for (var c = 0f; c <= 1f; c += 0.25f)
            for (var f = 0f; f <= 1f; f += 0.25f)
            {
                TerrainMacroVariation.Evaluate(s, m, c, f, 1f, out var br, out var r, out var g, out var b);
                foreach (var v in new[] { br, r, g, b })
                    Assert.IsTrue(v >= TerrainMacroVariation.MinMult && v <= TerrainMacroVariation.MaxMult, "mult " + v);
                var total = br * g;
                Assert.IsTrue(total >= TerrainMacroVariation.MinMult && total <= TerrainMacroVariation.MaxMult, "total " + total);
            }
        }

        [Test]
        public void Evaluate_StrawWarmerMoistDarker()
        {
            TerrainMacroVariation.Evaluate(1f, 0f, 0f, 0f, 1f, out _, out var rs, out _, out var bs);
            Assert.Greater(rs, bs);
            TerrainMacroVariation.Evaluate(0f, -1f, 0f, 0f, 1f, out var bm, out _, out _, out _);
            Assert.AreEqual(0.9f, bm, 0.02f);
        }

        [Test]
        public void Evaluate_ProtectedIsNeutral_AndDeterministic()
        {
            TerrainMacroVariation.Evaluate(1f, -1f, 1f, 1f, 0f, out var br, out var r, out var g, out var b);
            Assert.AreEqual(1f, br, 1e-5f); Assert.AreEqual(1f, r, 1e-5f);
            Assert.AreEqual(1f, g, 1e-5f); Assert.AreEqual(1f, b, 1e-5f);
            Assert.AreEqual(0f, TerrainMacroVariation.Protection(1f, 0f));
            Assert.AreEqual(1f, TerrainMacroVariation.Protection(0f, 0f));
            TerrainMacroVariation.Evaluate(0.7f, 0.2f, 0.9f, 0.8f, 1f, out var a1, out _, out _, out _);
            TerrainMacroVariation.Evaluate(0.7f, 0.2f, 0.9f, 0.8f, 1f, out var a2, out _, out _, out _);
            Assert.AreEqual(a1, a2);
        }
    }
}
