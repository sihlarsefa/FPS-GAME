using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class AntiAliasingMathTests
    {
        [Test]
        public void DesiredMode_PerTier()
        {
            Assert.AreEqual(AaMode.Fxaa, AntiAliasingMath.DesiredMode(0));
            Assert.AreEqual(AaMode.SmaaHigh, AntiAliasingMath.DesiredMode(1));
            Assert.AreEqual(AaMode.Taa, AntiAliasingMath.DesiredMode(2));
            Assert.AreEqual(AaMode.Taa, AntiAliasingMath.DesiredMode(3));
            Assert.AreEqual(AaMode.Fxaa, AntiAliasingMath.DesiredMode(-4));
            Assert.AreEqual(AaMode.Taa, AntiAliasingMath.DesiredMode(99));
        }

        [Test]
        public void ForTier_StpUpscalerTurnsTaaIntoStp_ButNotLowTiers()
        {
            Assert.AreEqual(AaMode.Stp, AntiAliasingMath.ForTier(3, true).Mode);
            Assert.AreEqual(AaMode.Taa, AntiAliasingMath.ForTier(3, false).Mode);
            Assert.AreEqual(AaMode.Fxaa, AntiAliasingMath.ForTier(0, true).Mode);
            Assert.IsTrue(AntiAliasingMath.ForTier(2, true).IsTemporal);
            Assert.IsFalse(AntiAliasingMath.ForTier(1, true).IsTemporal);
        }

        [Test]
        public void Effective_OverlayStackFallsBackToSmaa()
        {
            Assert.AreEqual(AaMode.SmaaHigh, AntiAliasingMath.Effective(AaMode.Stp, true, false));
            Assert.AreEqual(AaMode.SmaaHigh, AntiAliasingMath.Effective(AaMode.Taa, true, false));
            Assert.AreEqual(AaMode.Taa, AntiAliasingMath.Effective(AaMode.Taa, false, false));
            Assert.AreEqual(AaMode.Taa, AntiAliasingMath.Effective(AaMode.Taa, true, true));
            Assert.AreEqual(AaMode.Fxaa, AntiAliasingMath.Effective(AaMode.Fxaa, true, false));
        }

        [Test]
        public void Sharpness_SliderScalesAndClamps()
        {
            Assert.AreEqual(0f, AntiAliasingMath.EffectiveSharpness(0.5f, 0f), 1e-6f);
            Assert.AreEqual(0.5f, AntiAliasingMath.EffectiveSharpness(0.5f, 0.5f), 1e-6f);
            Assert.AreEqual(1f, AntiAliasingMath.EffectiveSharpness(0.9f, 1f), 1e-6f);
            Assert.AreEqual(0f, AntiAliasingMath.EffectiveSharpness(-1f, 1f), 1e-6f);
            Assert.IsFalse(AntiAliasingMath.SharpenWorthRunning(0.01f));
            Assert.IsTrue(AntiAliasingMath.SharpenWorthRunning(0.3f));
        }

        [Test]
        public void MipBias_NegativeWhenUpscaledOrTemporal_ZeroNativeNonTemporal()
        {
            Assert.AreEqual(0f, AntiAliasingMath.MipBias(1f, AaMode.Fxaa), 1e-6f);
            Assert.AreEqual(-0.25f, AntiAliasingMath.MipBias(1f, AaMode.Stp), 1e-6f);
            Assert.AreEqual(-0.58f, AntiAliasingMath.MipBias(0.67f, AaMode.Stp), 0.01f);
            Assert.Less(AntiAliasingMath.MipBias(0.77f, AaMode.Fxaa), 0f);
            Assert.AreEqual(-1f, AntiAliasingMath.MipBias(0.1f, AaMode.Taa), 1e-6f);
            Assert.AreEqual(0f, AntiAliasingMath.MipBias(0f, AaMode.None), 1e-6f);
        }

        [Test]
        public void SharpenBase_WithinRange()
        {
            for (var t = 0; t < 4; t++)
                Assert.That(AntiAliasingMath.SharpenBaseFor(t), Is.InRange(0.1f, 0.8f));
        }
    }
}
