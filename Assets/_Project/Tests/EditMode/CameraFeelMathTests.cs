#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Player;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CameraFeelMathTests
    {
        [Test]
        public void SlideSpeed_DecaysToCrouchSpeed()
        {
            Assert.AreEqual(9f, CameraFeelMath.SlideSpeed(9f, 2.4f, 0f, 0.75f), 1e-4f);
            Assert.AreEqual(2.4f, CameraFeelMath.SlideSpeed(9f, 2.4f, 0.75f, 0.75f), 1e-4f);
            Assert.Less(CameraFeelMath.SlideSpeed(9f, 2.4f, 0.3f, 0.75f), 9f);
        }

        [Test]
        public void Mantle_HeightRange_AndDuration()
        {
            Assert.IsFalse(CameraFeelMath.MantleHeightValid(0.3f));
            Assert.IsTrue(CameraFeelMath.MantleHeightValid(1.2f));
            Assert.IsFalse(CameraFeelMath.MantleHeightValid(1.5f));
            Assert.Greater(CameraFeelMath.MantleDuration(1.2f), CameraFeelMath.MantleDuration(0.5f));
            Assert.AreEqual(1f, CameraFeelMath.MantleRise(1f), 1e-4f);
            Assert.AreEqual(0f, CameraFeelMath.MantleForward(0.2f), 1e-4f);
            Assert.AreEqual(1f, CameraFeelMath.MantleForward(1f), 1e-4f);
        }

        [Test]
        public void SprintFov_IsSixDegrees()
        {
            Assert.AreEqual(6f, CameraFeelMath.SprintFovOffset(1f), 1e-4f);
            Assert.AreEqual(0f, CameraFeelMath.SprintFovOffset(-2f), 1e-4f);
        }

        [Test]
        public void LandingDip_ProportionalAndCapped()
        {
            Assert.AreEqual(0.06f, CameraFeelMath.LandingDip(5f, 0.012f, 0.16f), 1e-4f);
            Assert.AreEqual(0.16f, CameraFeelMath.LandingDip(40f, 0.012f, 0.16f), 1e-4f);
            Assert.AreEqual(0f, CameraFeelMath.LandingDip(float.NaN, 0.012f, 0.16f));
        }

        [Test]
        public void DirectionalPunch_SideSignsAndExplosionFalloff()
        {
            CameraFeelMath.DirectionalPunch(1f, 0f, 1f, out _, out var yawR, out var rollR);
            CameraFeelMath.DirectionalPunch(-1f, 0f, 1f, out _, out var yawL, out var rollL);
            Assert.Less(yawR, 0f);
            Assert.Greater(yawL, 0f);
            Assert.Less(rollR, 0f);
            Assert.Greater(rollL, 0f);
            Assert.AreEqual(0f, CameraFeelMath.ExplosionIntensity(50f, 10f));
            Assert.Greater(CameraFeelMath.ExplosionIntensity(2f, 10f), CameraFeelMath.ExplosionIntensity(8f, 10f));
        }

        [Test]
        public void CameraShakeSetting_DefaultsToOne()
        {
            Assert.AreEqual(1f, new GameSettings().CameraShakeIntensity);
        }
    }
}
#endif
