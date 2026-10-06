#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vehicles;
using Rules = Project.Infrastructure.Vehicles.HelicopterFlightRules;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class HelicopterFlightRulesTests
    {
        [Test]
        public void Rotor_SpinsUpAndDownGradually()
        {
            var r = 0f;
            for (var i = 0; i < 30; i++) r = Rules.StepRotor(r, true, 0.1f);
            Assert.AreEqual(0.5f, r, 0.02f);
            for (var i = 0; i < 200; i++) r = Rules.StepRotor(r, true, 0.1f);
            Assert.AreEqual(1f, r, 1e-4f);
            r = Rules.StepRotor(r, false, 6f);
            Assert.AreEqual(0.5f, r, 0.02f);
            Assert.AreEqual(0f, Rules.StepRotor(float.NaN, true, 0f));
        }

        [Test]
        public void Lift_RequiresFullRotor()
        {
            Assert.Less(Rules.LiftAcceleration(1f, 0.6f, 50f, 7f), Rules.Gravity);
            Assert.AreEqual(Rules.Gravity, Rules.LiftAcceleration(Rules.HoverCollective, 1f, 50f, 7f), 1e-3f);
            Assert.Greater(Rules.LiftAcceleration(0.8f, 1f, 50f, 7f), Rules.Gravity);
        }

        [Test]
        public void GroundEffect_BoostsNearGroundOnly()
        {
            Assert.AreEqual(1.25f, Rules.GroundEffect(0f, 7f), 1e-4f);
            Assert.AreEqual(1f, Rules.GroundEffect(7f, 7f), 1e-4f);
            Assert.AreEqual(1f, Rules.GroundEffect(30f, 7f), 1e-4f);
            Assert.Greater(Rules.GroundEffect(2f, 7f), Rules.GroundEffect(5f, 7f));
        }

        [Test]
        public void Collective_ClampedAndAutoHoverCorrectsSink()
        {
            Assert.AreEqual(1f, Rules.StepCollective(0.99f, 1f, 1f), 1e-4f);
            Assert.AreEqual(0f, Rules.StepCollective(0.01f, -1f, 1f), 1e-4f);
            Assert.AreEqual(Rules.HoverCollective, Rules.AutoHoverCollective(0f), 1e-4f);
            Assert.Greater(Rules.AutoHoverCollective(-4f), Rules.HoverCollective);
            Assert.Less(Rules.AutoHoverCollective(4f), Rules.HoverCollective);
            Assert.Less(Rules.UnpilotedCollective(0f), Rules.HoverCollective);
        }

        [Test]
        public void Stick_IntegratesAndRecenters()
        {
            var s = Rules.StepStick(0f, 50f, 0.016f);
            Assert.Greater(s, 0f);
            for (var i = 0; i < 400; i++) s = Rules.StepStick(s, 0f, 0.016f);
            Assert.AreEqual(0f, s, 1e-4f);
            Assert.AreEqual(1f, Rules.StepStick(0.9f, 1000f, 0.016f), 1e-4f);
            Assert.AreEqual(Rules.MaxRollDeg, Rules.TargetRoll(5f), 1e-4f);
        }

        [Test]
        public void CrashDamage_SoftLandingSafeHardLandingHurts()
        {
            Assert.AreEqual(0f, Rules.CrashDamage(3f, 0f));
            var medium = Rules.CrashDamage(5.5f, 5f);
            var hard = Rules.CrashDamage(9f, 5f);
            Assert.Greater(medium, 0f);
            Assert.Greater(hard, medium);
            Assert.Greater(Rules.CrashDamage(9f, 40f), hard);
        }

        [Test]
        public void RotorStrike_NoDamageWhenStopped()
        {
            Assert.AreEqual(0f, Rules.RotorStrikeDamage(0.1f));
            Assert.Greater(Rules.RotorStrikeDamage(1f), Rules.RotorStrikeDamage(0.5f));
        }

        [Test]
        public void PickSeat_PilotThenPassengerThenGunner()
        {
            int idx;
            Assert.AreEqual(Rules.SeatKind.Pilot, Rules.PickSeat(true, new bool[9], new bool[2], out idx));
            Assert.AreEqual(Rules.SeatKind.Passenger,
                Rules.PickSeat(false, new[] { true, false }, new bool[2], out idx));
            Assert.AreEqual(1, idx);
            Assert.AreEqual(Rules.SeatKind.Gunner,
                Rules.PickSeat(false, new[] { true }, new[] { true, false }, out idx));
            Assert.AreEqual(1, idx);
            Assert.AreEqual(Rules.SeatKind.None, Rules.PickSeat(false, new[] { true }, new[] { true }, out idx));
        }

        [Test]
        public void IsLanded_NeedsLowAndSlow()
        {
            Assert.IsTrue(Rules.IsLanded(0.1f, 0.2f));
            Assert.IsFalse(Rules.IsLanded(5f, 0f));
            Assert.IsFalse(Rules.IsLanded(0.1f, -3f));
        }
    }
}
#endif
