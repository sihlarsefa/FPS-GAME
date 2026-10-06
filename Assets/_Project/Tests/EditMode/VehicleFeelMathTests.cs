using NUnit.Framework;
using Project.Infrastructure.Vehicles;

namespace Project.Tests.EditMode
{
    public sealed class VehicleFeelMathTests
    {
        [Test]
        public void Gear_IncreasesWithSpeed_AndStaysInRange()
        {
            var prev = 0;
            for (var v = 0f; v <= 100f; v += 5f)
            {
                var g = VehicleFeelMath.GearFor(v, 85f);
                Assert.GreaterOrEqual(g, prev);
                Assert.That(g, Is.InRange(1, VehicleFeelMath.GearCount));
                prev = g;
            }

            Assert.AreEqual(1, VehicleFeelMath.GearFor(0f, 85f));
            Assert.AreEqual(VehicleFeelMath.GearCount, VehicleFeelMath.GearFor(85f, 85f));
        }

        [Test]
        public void Rpm_DropsAtGearChange_AndStaysBounded()
        {
            var below = VehicleFeelMath.Rpm01(85f * VehicleFeelMath.GearUpper(1) - 0.2f, 85f);
            var above = VehicleFeelMath.Rpm01(85f * VehicleFeelMath.GearUpper(1) + 0.2f, 85f);
            Assert.Greater(below, above);
            for (var v = 0f; v <= 120f; v += 3f)
                Assert.That(VehicleFeelMath.Rpm01(v, 85f), Is.InRange(0.3f, 1f));
        }

        [Test]
        public void BodyTilt_NoseUpOnAccel_RollsOutwardInTurn_AndClamped()
        {
            Assert.Less(VehicleFeelMath.BodyPitchTarget(5f), 0f);
            Assert.Greater(VehicleFeelMath.BodyPitchTarget(-5f), 0f);
            Assert.Greater(VehicleFeelMath.BodyRollTarget(4f), 0f);
            Assert.LessOrEqual(System.Math.Abs(VehicleFeelMath.BodyPitchTarget(999f)), VehicleFeelMath.MaxBodyPitch + 0.001f);
            Assert.LessOrEqual(System.Math.Abs(VehicleFeelMath.BodyRollTarget(-999f)), VehicleFeelMath.MaxBodyRoll + 0.001f);
        }

        [Test]
        public void Traverse_IsRateLimited_AndWrapsShortestWay()
        {
            Assert.AreEqual(7f, VehicleFeelMath.TraverseStep(0f, 90f, 70f, 0.1f), 0.001f);
            Assert.AreEqual(90f, VehicleFeelMath.TraverseStep(89f, 90f, 70f, 0.1f), 0.001f);
            Assert.Less(VehicleFeelMath.TraverseStep(-170f, 170f, 70f, 0.1f), -170f);
        }

        [Test]
        public void Whine_ScalesWithRate()
        {
            Assert.AreEqual(0f, VehicleFeelMath.WhineLevel(0f, 0f), 0.001f);
            Assert.AreEqual(1f, VehicleFeelMath.WhineLevel(VehicleFeelMath.YawDegPerSec * 3f, 0f), 0.001f);
            Assert.AreEqual(0.5f, VehicleFeelMath.WhineLevel(VehicleFeelMath.YawDegPerSec * 0.5f, 0f), 0.001f);
        }

        [Test]
        public void DamageState_And_Zones_Progress()
        {
            Assert.AreEqual(VehicleDamageState.Saglam, VehicleFeelMath.DamageStateFor(0.9f, false));
            Assert.AreEqual(VehicleDamageState.Hafif, VehicleFeelMath.DamageStateFor(0.5f, false));
            Assert.AreEqual(VehicleDamageState.Agir, VehicleFeelMath.DamageStateFor(0.25f, false));
            Assert.AreEqual(VehicleDamageState.Yangin, VehicleFeelMath.DamageStateFor(0.1f, false));
            Assert.AreEqual(VehicleDamageState.Yangin, VehicleFeelMath.DamageStateFor(0.9f, true));
            Assert.AreEqual(0f, VehicleFeelMath.SmokeInterval(VehicleDamageState.Saglam));
            Assert.Greater(VehicleFeelMath.SmokeInterval(VehicleDamageState.Hafif), VehicleFeelMath.SmokeInterval(VehicleDamageState.Yangin));
            Assert.AreEqual(0, VehicleFeelMath.ZoneState(0, 1f));
            Assert.AreEqual(1, VehicleFeelMath.ZoneState(0, 0.5f));
            Assert.AreEqual(2, VehicleFeelMath.ZoneState(3, 0.05f));
        }

        [Test]
        public void Heading_Labels()
        {
            Assert.AreEqual("K 000", VehicleFeelMath.HeadingLabel(0f));
            Assert.AreEqual("D 090", VehicleFeelMath.HeadingLabel(90f));
            Assert.AreEqual("KB 315", VehicleFeelMath.HeadingLabel(-45f));
            Assert.AreEqual("K 000", VehicleFeelMath.HeadingLabel(359.8f));
        }

        [Test]
        public void EnginePitch_RisesWithRpm_AndDipsOnShift()
        {
            Assert.Greater(VehicleFeelMath.EnginePitch(1f, 1f, 0f), VehicleFeelMath.EnginePitch(0.3f, 0f, 0f));
            Assert.Less(VehicleFeelMath.EnginePitch(0.8f, 1f, 1f), VehicleFeelMath.EnginePitch(0.8f, 1f, 0f));
        }
    }
}
