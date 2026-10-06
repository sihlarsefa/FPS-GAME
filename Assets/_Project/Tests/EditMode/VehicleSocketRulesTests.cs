using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    public class VehicleSocketRulesTests
    {
        [Test]
        public void Normalize_StripsBlenderAndCloneSuffix()
        {
            Assert.AreEqual("rotor_main", VehicleSocketRules.Normalize("Rotor_Main.001"));
            Assert.AreEqual("wheel_fl", VehicleSocketRules.Normalize("Wheel_FL(Clone)"));
        }

        [Test]
        public void Matches_AcceptsLegacyAliases()
        {
            Assert.IsTrue(VehicleSocketRules.Matches("MainRotor", VehicleSocketRules.MainRotorNames()));
            Assert.IsTrue(VehicleSocketRules.Matches("rotor_tail", VehicleSocketRules.TailRotorNames()));
            Assert.IsTrue(VehicleSocketRules.Matches("Wheel_2", VehicleSocketRules.WheelAliases(2)));
            Assert.IsTrue(VehicleSocketRules.Matches("Wheel_RR", VehicleSocketRules.WheelAliases(3)));
            Assert.IsFalse(VehicleSocketRules.Matches("Wheel_RR", VehicleSocketRules.WheelAliases(0)));
        }

        [Test]
        public void SeatAliases_ZeroAndOneBased()
        {
            Assert.IsTrue(VehicleSocketRules.Matches("Seat_0", VehicleSocketRules.SeatAliases(0)));
            Assert.IsTrue(VehicleSocketRules.Matches("Seat_03", VehicleSocketRules.SeatAliases(2)));
        }

        [Test]
        public void SeatTrust_RejectsFarPoints()
        {
            Assert.IsTrue(VehicleSocketRules.IsSeatTrustworthy(0.2f, 0.1f, 0.3f));
            Assert.IsFalse(VehicleSocketRules.IsSeatTrustworthy(3f, 0f, 0f));
            Assert.IsFalse(VehicleSocketRules.IsSeatTrustworthy(float.NaN, 0f, 0f));
        }

        [Test]
        public void BlurVisible_HasHysteresis()
        {
            Assert.IsFalse(VehicleSocketRules.BlurVisible(0.5f, false));
            Assert.IsTrue(VehicleSocketRules.BlurVisible(0.5f, true));
            Assert.IsTrue(VehicleSocketRules.BlurVisible(0.7f, false));
            Assert.IsFalse(VehicleSocketRules.BlurVisible(0.3f, true));
        }

        [Test]
        public void Condition_FromHealthAndTint()
        {
            Assert.AreEqual(VehicleCondition.Clean, VehicleSocketRules.ConditionFor(0.9f, false));
            Assert.AreEqual(VehicleCondition.Dirty, VehicleSocketRules.ConditionFor(0.3f, false));
            Assert.AreEqual(VehicleCondition.Burnt, VehicleSocketRules.ConditionFor(0.8f, true));
            VehicleSocketRules.ConditionTint(VehicleCondition.Burnt, out var r, out _, out _, out var s);
            Assert.IsTrue(r < 0.3f && s < 0.5f);
            VehicleSocketRules.ConditionTint(VehicleCondition.Clean, out r, out _, out _, out s);
            Assert.AreEqual(1f, r);
            Assert.AreEqual(1f, s);
        }
    }
}
