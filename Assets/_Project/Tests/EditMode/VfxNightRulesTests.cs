#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class VfxNightRulesTests
    {
        [Test]
        public void Tracer_EveryThird_AndAllMachineGun()
        {
            var rifle = 0;
            var mg = 0;
            for (var i = 1; i <= 30; i++)
            {
                if (VfxNightRules.ShouldDrawTracer(i, CaliberClass.Rifle)) rifle++;
                if (VfxNightRules.ShouldDrawTracer(i, CaliberClass.MachineGun)) mg++;
            }

            Assert.AreEqual(10, rifle);
            Assert.AreEqual(30, mg);
            Assert.IsFalse(VfxNightRules.ShouldDrawTracer(3, CaliberClass.Explosion));
        }

        [Test]
        public void Night_IsBrighter()
        {
            Assert.Greater(VfxNightRules.NightFactor(TimeOfDay.Gece), VfxNightRules.NightFactor(TimeOfDay.Aksam));
            Assert.AreEqual(0f, VfxNightRules.NightFactor(TimeOfDay.Gunduz));
            Assert.Greater(VfxNightRules.TracerHdr(1f), VfxNightRules.TracerHdr(0f));
            Assert.Greater(VfxNightRules.TracerHdr(0f), 1f);
            Assert.Greater(VfxNightRules.ExplosionIntensityScale(1f), 1f);
        }

        [Test]
        public void MuzzleLightCap_GrowsWithTier_AndFlickerBounded()
        {
            Assert.AreEqual(1, VfxNightRules.MuzzleLightCap(VfxTier.Low));
            Assert.AreEqual(3, VfxNightRules.MuzzleLightCap(VfxTier.High));
            Assert.Greater(VfxNightRules.MuzzleLightScale(1f, 0.5f), VfxNightRules.MuzzleLightScale(0f, 0.5f));
            Assert.GreaterOrEqual(VfxNightRules.MuzzleLightScale(0f, 0f), 0.75f);
            Assert.LessOrEqual(VfxNightRules.MuzzleLightScale(0f, 1f), 1.25f);
        }

        [Test]
        public void TracerLength_ClampedAndPositive()
        {
            Assert.AreEqual(60f, VfxNightRules.TracerLength(100000f, 0.02f));
            Assert.AreEqual(6f, VfxNightRules.TracerLength(float.NaN, 0.02f));
            Assert.AreEqual(18f, VfxNightRules.TracerLength(900f, 0.02f), 0.001f);
        }

        [Test]
        public void SmokeGlow_InsideBrightens_OutsideZero()
        {
            Assert.AreEqual(0f, VfxNightRules.SmokeGlow(30f, 8f, 1f, 1f));
            Assert.Greater(VfxNightRules.SmokeGlow(2f, 8f, 1f, 1f), VfxNightRules.SmokeGlow(10f, 8f, 1f, 1f));
            Assert.Greater(VfxNightRules.SmokeGlow(2f, 8f, 1f, 1f), VfxNightRules.SmokeGlow(2f, 8f, 1f, 0f));
            Assert.AreEqual(0f, VfxNightRules.SmokeGlow(2f, 0f, 1f, 1f));
        }
    }
}
#endif
