using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class ScreenSpaceMathTests
    {
        [Test]
        public void Tiers_LowIsOff_ContactFromMedium_SsrOnlyHighUltra()
        {
            Assert.IsFalse(ScreenSpaceMath.ContactShadowsForTier(0).Enabled);
            Assert.IsFalse(ScreenSpaceMath.SsrForTier(0).Enabled);
            Assert.IsFalse(ScreenSpaceMath.SsrForTier(1).Enabled);
            Assert.IsTrue(ScreenSpaceMath.ContactShadowsForTier(1).Enabled);
            Assert.IsTrue(ScreenSpaceMath.SsrForTier(2).Enabled);
            Assert.IsTrue(ScreenSpaceMath.SsrForTier(3).Enabled);
        }

        [Test]
        public void Tiers_CostGrowsWithTier_AndClamps()
        {
            Assert.Less(ScreenSpaceMath.ContactShadowsForTier(1).Steps, ScreenSpaceMath.ContactShadowsForTier(3).Steps);
            Assert.Less(ScreenSpaceMath.SsrForTier(2).MaxSteps, ScreenSpaceMath.SsrForTier(3).MaxSteps);
            Assert.AreEqual(ScreenSpaceMath.SsrForTier(3).MaxSteps, ScreenSpaceMath.SsrForTier(99).MaxSteps);
            Assert.IsFalse(ScreenSpaceMath.SsrForTier(-5).Enabled);
            Assert.AreEqual(2, ScreenSpaceMath.ContactShadowsForTier(2).ResolutionDivisor);
        }

        [Test]
        public void ContactDistanceFade_FullNearZeroFar()
        {
            Assert.AreEqual(1f, ScreenSpaceMath.ContactDistanceFade(5f, 40f), 1e-4f);
            Assert.AreEqual(0f, ScreenSpaceMath.ContactDistanceFade(40f, 40f), 1e-4f);
            Assert.AreEqual(0f, ScreenSpaceMath.ContactDistanceFade(10f, 0f), 1e-4f);
            Assert.Less(ScreenSpaceMath.ContactDistanceFade(35f, 40f), ScreenSpaceMath.ContactDistanceFade(28f, 40f));
        }

        [Test]
        public void ContactHit_BehindSurfaceWithinThickness()
        {
            Assert.IsTrue(ScreenSpaceMath.ContactHit(10.2f, 10f, 10f, 0.4f));
            Assert.IsFalse(ScreenSpaceMath.ContactHit(9.9f, 10f, 10f, 0.4f));   // yüzeyin önünde
            Assert.IsFalse(ScreenSpaceMath.ContactHit(11f, 10f, 10f, 0.4f));    // çok arkada: ince değil
            Assert.IsFalse(ScreenSpaceMath.ContactHit(10.005f, 10f, 10f, 0.4f)); // bias altında: kendi yüzeyi
        }

        [Test]
        public void ContactApplyFactor_NoDoubleDarkeningInShadowMap()
        {
            Assert.AreEqual(1f, ScreenSpaceMath.ContactApplyFactor(0f, 0.8f, 0f), 1e-5f);
            Assert.AreEqual(0.2f, ScreenSpaceMath.ContactApplyFactor(0f, 0.8f, 1f), 1e-5f);
            Assert.AreEqual(1f, ScreenSpaceMath.ContactApplyFactor(1f, 0.8f, 1f), 1e-5f);
        }

        [Test]
        public void Smoothness_CutoffRejectsRough()
        {
            Assert.AreEqual(0f, ScreenSpaceMath.SmoothnessWeight(0.2f, 0.45f, 0.75f), 1e-5f);
            Assert.AreEqual(1f, ScreenSpaceMath.SmoothnessWeight(0.9f, 0.45f, 0.75f), 1e-5f);
            var mid = ScreenSpaceMath.SmoothnessWeight(0.6f, 0.45f, 0.75f);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
        }

        [Test]
        public void WetSmoothness_OnlyUpFacingAndWet()
        {
            Assert.AreEqual(0f, ScreenSpaceMath.WetSmoothness(1f, 0f), 1e-5f);
            Assert.AreEqual(0f, ScreenSpaceMath.WetSmoothness(0f, 1f), 1e-5f);
            Assert.AreEqual(1f, ScreenSpaceMath.WetSmoothness(1f, 1f), 1e-5f);
        }

        [Test]
        public void EdgeFade_ZeroAtEdgeOneInside()
        {
            Assert.AreEqual(0f, ScreenSpaceMath.ScreenEdgeFade(0f, 0.5f, 0.1f), 1e-5f);
            Assert.AreEqual(1f, ScreenSpaceMath.ScreenEdgeFade(0.5f, 0.5f, 0.1f), 1e-5f);
            Assert.AreEqual(0f, ScreenSpaceMath.ScreenEdgeFade(1.2f, 0.5f, 0f), 1e-5f);
        }

        [Test]
        public void Fresnel_GrazingBrighter()
        {
            Assert.AreEqual(0.04f, ScreenSpaceMath.Fresnel(1f, 0.04f), 1e-5f);
            Assert.Greater(ScreenSpaceMath.Fresnel(0.1f, 0.04f), ScreenSpaceMath.Fresnel(0.9f, 0.04f));
            Assert.AreEqual(1f, ScreenSpaceMath.Fresnel(0f, 0.04f), 1e-5f);
        }

        [Test]
        public void SsrFades_Behave()
        {
            Assert.AreEqual(1f, ScreenSpaceMath.SsrDistanceFade(0f, 50f), 1e-5f);
            Assert.AreEqual(0f, ScreenSpaceMath.SsrDistanceFade(80f, 50f), 1e-5f);
            Assert.AreEqual(1f, ScreenSpaceMath.TowardCameraFade(-0.8f), 1e-5f);
            Assert.AreEqual(0f, ScreenSpaceMath.TowardCameraFade(0.5f), 1e-5f);
            Assert.IsTrue(ScreenSpaceMath.RejectBackfaceHit(0.6f));
            Assert.IsFalse(ScreenSpaceMath.RejectBackfaceHit(-0.6f));
            Assert.Less(ScreenSpaceMath.SoftClampHdr(100f), 100f);
            Assert.AreEqual(0f, ScreenSpaceMath.SoftClampHdr(0f), 1e-6f);
        }

        // Dikey bir duvar: u > 0.6 olan piksellerde göz derinliği 10, aksi halde 30 (uzak zemin).
        private static float Wall(float u, float v) => u > 0.6f ? 10f : 30f;

        [Test]
        public void March_HitsWallAndRefines()
        {
            // Işın w=5'ten w=20'ye giderken (kameradan uzaklaşır), u 0.2 -> 0.9: duvar derinliği 10'u geçtiği yerde isabet.
            var r = ScreenSpaceMath.MarchScreenRay(0.2f, 0.5f, 5f, 0.9f, 0.5f, 20f, 40, 4f, 1920f, 1080f, 0.5f, 0.5f, 0.02f, 0.02f, 0.002f, 5, Wall);
            Assert.IsTrue(r.Hit);
            Assert.Greater(r.U, 0.6f);
            Assert.Less(r.U, 0.9f);
            Assert.Less(r.EyeDepth, 12f);
            Assert.Greater(r.EyeDepth, 8f);
        }

        [Test]
        public void March_MissesWhenRayStaysInFrontOfSurfaces()
        {
            // Işın sürekli kameraya yakın (w=2..3) kalır: sahne 10/30'da, hiçbir zaman arkasına geçmez.
            var r = ScreenSpaceMath.MarchScreenRay(0.2f, 0.5f, 2f, 0.9f, 0.5f, 3f, 40, 4f, 1920f, 1080f, 0.5f, 0.5f, 0.02f, 0.02f, 0.002f, 5, Wall);
            Assert.IsFalse(r.Hit);
        }

        [Test]
        public void March_SkipsThinOccluder_AndStopsAtScreenEdge()
        {
            // Çok ince örtücü (kalınlık 0.5): ışın 10 -> 60 derinlikten atlayınca "arkasından geçer".
            System.Func<float, float, float> thin = (u, v) => 5f;
            var r = ScreenSpaceMath.MarchScreenRay(0.5f, 0.5f, 1f, 0.5f, 0.5f, 50f, 8, 4f, 1920f, 1080f, 0.5f, 0.5f, 0.02f, 0.02f, 0.002f, 3, thin);
            Assert.IsFalse(r.Hit);           // piksel uzunluğu ~0: ışın tek noktada
            var r2 = ScreenSpaceMath.MarchScreenRay(0.5f, 0.5f, 10f, 1.5f, 0.5f, 10f, 40, 4f, 1920f, 1080f, 0.5f, 0.5f, 0.02f, 0.02f, 0.002f, 3, (u, v) => 1000f);
            Assert.IsFalse(r2.Hit);          // ekran dışına çıkar, gökyüzü (sonsuz) isabet vermez
        }

        [Test]
        public void March_NullSceneOrBadDepth_DoesNotThrow()
        {
            Assert.IsFalse(ScreenSpaceMath.MarchScreenRay(0f, 0f, 1f, 1f, 1f, 1f, 8, 4f, 100f, 100f, 0f, 0.1f, 0f, 0f, 0f, 1, null).Hit);
            Assert.IsFalse(ScreenSpaceMath.MarchScreenRay(0f, 0f, 0f, 1f, 1f, 1f, 8, 4f, 100f, 100f, 0f, 0.1f, 0f, 0f, 0f, 1, Wall).Hit);
        }
    }
}
