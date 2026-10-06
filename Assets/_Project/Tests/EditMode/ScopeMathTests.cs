#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Weapons;

namespace Project.Tests.EditMode
{
    public sealed class ScopeMathTests
    {
        [Test]
        public void Fov_Decreases_WithMagnification()
        {
            var f1 = ScopeMath.FovFromMagnification(70f, 1f);
            var f4 = ScopeMath.FovFromMagnification(70f, 4f);
            Assert.IsTrue(System.Math.Abs(f1 - 70f) < 0.01f);
            Assert.IsTrue(f4 < 20f && f4 > 10f);
            Assert.IsTrue(ScopeMath.FovFromMagnification(70f, 1000f) >= ScopeMath.MinFovDegrees);
        }

        [Test]
        public void Fov_HandlesNaN()
        {
            var f = ScopeMath.FovFromMagnification(float.NaN, float.NaN);
            Assert.IsTrue(f > 0f && f <= 120f);
        }

        [Test]
        public void ZeroingAngle_GrowsWithRange_AndShrinksWithVelocity()
        {
            var a100 = ScopeMath.ZeroingAngleDegrees(100f, 800f);
            var a500 = ScopeMath.ZeroingAngleDegrees(500f, 800f);
            Assert.IsTrue(a500 > a100 && a100 > 0f);
            Assert.IsTrue(ScopeMath.ZeroingAngleDegrees(500f, 400f) > a500);
            // 500 m @ 800 m/s: asin(9.81*500/640000)/2 ~ 0.22 derece.
            Assert.IsTrue(System.Math.Abs(a500 - 0.22f) < 0.02f);
        }

        [Test]
        public void ZeroingAngle_Unreachable_Caps45()
        {
            Assert.AreEqual(45f, ScopeMath.ZeroingAngleDegrees(100000f, 100f));
            Assert.AreEqual(0f, ScopeMath.ZeroingAngleDegrees(0f, 800f));
        }

        [Test]
        public void Drop_IsZero_AtZeroingRange()
        {
            var ang = ScopeMath.ZeroingAngleDegrees(300f, 800f);
            var drop = ScopeMath.DropAtRange(300f, 800f, ang);
            Assert.IsTrue(System.Math.Abs(drop) < 0.05f);
            Assert.IsTrue(ScopeMath.DropAtRange(500f, 800f, ang) < -0.5f);
        }

        [Test]
        public void ZeroingSteps_ClampAtEnds()
        {
            Assert.AreEqual(0, ScopeMath.StepZeroing(0, -1));
            Assert.AreEqual(4, ScopeMath.StepZeroing(4, +1));
            Assert.AreEqual(2, ScopeMath.StepZeroing(1, +1));
            Assert.AreEqual(2, ScopeMath.NearestZeroingIndex(310f));
        }

        [Test]
        public void Magnification_StepsAndClamps()
        {
            var m = ScopeMath.StepMagnification(6f, 1f, 3f, 9f);
            Assert.IsTrue(m > 6f);
            Assert.AreEqual(9f, ScopeMath.StepMagnification(9f, 1f, 3f, 9f));
            Assert.AreEqual(3f, ScopeMath.StepMagnification(3f, -1f, 3f, 9f));
            Assert.AreEqual(2f, ScopeMath.StepMagnification(2f, 1f, 2f, 2f));
        }

        [Test]
        public void Reticle_Selection()
        {
            Assert.AreEqual(ReticleKind.None, ScopeMath.SelectReticle(1f, false));
            Assert.AreEqual(ReticleKind.RedDot, ScopeMath.SelectReticle(1.35f, false));
            Assert.AreEqual(ReticleKind.AcogChevron, ScopeMath.SelectReticle(3f, true));
            Assert.AreEqual(ReticleKind.MilDot, ScopeMath.SelectReticle(6f, true));
        }

        [Test]
        public void TierPlan_PipOnlyOnHighTiers()
        {
            Assert.IsFalse(ScopeMath.TierPlan(0).PictureInPicture);
            Assert.IsFalse(ScopeMath.TierPlan(1).PictureInPicture);
            Assert.IsTrue(ScopeMath.TierPlan(2).PictureInPicture);
            Assert.IsTrue(ScopeMath.TierPlan(3).RenderTextureSize > ScopeMath.TierPlan(2).RenderTextureSize);
        }

        [Test]
        public void EyeBox_DarkensWithSway()
        {
            ScopeMath.EyeBox(0f, 0f, 0f, out _, out _, out var d0);
            ScopeMath.EyeBox(0.8f, 0.2f, 0.5f, out var sx, out _, out var d1);
            Assert.IsTrue(d1 > d0);
            Assert.IsTrue(sx > 0f && d1 <= 1f);
        }

        [Test]
        public void Shake_HoldVsExhausted()
        {
            Assert.IsTrue(ScopeMath.ShakeAmplitude(true, false, 2f, 4f) < ScopeMath.ShakeAmplitude(false, false, 4f, 4f));
            Assert.IsTrue(ScopeMath.ShakeAmplitude(false, true, 0f, 4f) > ScopeMath.ShakeAmplitude(false, false, 1f, 4f));
        }

        [Test]
        public void Glint_FacesObserverAndSun()
        {
            // Gözlemci +Z'de, dürbün +Z'ye bakıyor, güneş de +Z tarafında.
            var on = ScopeMath.GlintIntensity(0, 0, 1, 0, 0, 1, 0, 0.3f, 0.95f, 200f);
            Assert.IsTrue(on > 0.5f);
            Assert.AreEqual(0f, ScopeMath.GlintIntensity(0, 0, -1, 0, 0, 1, 0, 0, 1, 200f));
            Assert.AreEqual(0f, ScopeMath.GlintIntensity(0, 0, 1, 0, 0, 1, 0, 0, -1, 200f));
            Assert.AreEqual(0f, ScopeMath.GlintIntensity(0, 0, 1, 0, 0, 1, 0, 0, 1, 2000f));
        }

        [Test]
        public void Scope_ZeroingAngle_UsesCatalog_AndSteps()
        {
            Scope.ResetAll();
            Assert.AreEqual(0f, Scope.ZeroingAngle("yok_silah"));
            Assert.AreEqual(0f, Scope.ZeroingAngle(null));
            var id = Project.Application.Catalogs.WeaponCatalog.All[0].WeaponId;
            Scope.StepZeroing(id, +1);
            Assert.IsTrue(Scope.ZeroingAngle(id) > 0f);
            Scope.ResetAll();
        }

        [Test]
        public void Reticle_Pixels_CenterDrawn()
        {
            var px = ScopeTextures.RenderReticle(ReticleKind.RedDot, 64);
            Assert.IsTrue(px[32 * 64 + 32].a > 0);
            Assert.AreEqual(0, px[2 * 64 + 2].a);
            var v = ScopeTextures.RenderVignette(32, 0.4f, 0.05f);
            Assert.AreEqual(0, v[16 * 32 + 16].a);
            Assert.AreEqual(255, v[0].a);
        }
    }
}
#endif
