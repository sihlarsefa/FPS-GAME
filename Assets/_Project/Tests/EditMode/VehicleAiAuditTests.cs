#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.AI;
using Project.Infrastructure.Vehicles;
using UnityEngine;

namespace Project.Tests.EditMode
{
    /// <summary>Araç/AI denetimi: mermi zırh payı ve iniş noktası dağılımı (saf mantık).</summary>
    [TestFixture]
    public sealed class VehicleAiAuditTests
    {
        [Test]
        public void BulletDamageToVehicle_AppliesArmorFactor()
        {
            Assert.AreEqual(30f * KirpiCrewRules.BulletArmorFactor, KirpiCrewRules.BulletDamageToVehicle(30f), 1e-4f);
            Assert.Less(KirpiCrewRules.BulletDamageToVehicle(30f), 30f);
        }

        [Test]
        public void BulletDamageToVehicle_IgnoresInvalidDamage()
        {
            Assert.AreEqual(0f, KirpiCrewRules.BulletDamageToVehicle(0f));
            Assert.AreEqual(0f, KirpiCrewRules.BulletDamageToVehicle(-5f));
            Assert.AreEqual(0f, KirpiCrewRules.BulletDamageToVehicle(float.NaN));
        }

        [Test]
        public void ExitPoint_AlternatesSidesAndSpreadsSeats()
        {
            var o = Vector3.zero;
            var left = BotVehicleBoarding.ExitPoint(o, Vector3.right, Vector3.forward, 1);
            var right = BotVehicleBoarding.ExitPoint(o, Vector3.right, Vector3.forward, 0);
            Assert.Less(left.x, 0f);
            Assert.Greater(right.x, 0f);

            for (var a = 0; a < 9; a++)
            for (var b = a + 1; b < 9; b++)
            {
                var pa = BotVehicleBoarding.ExitPoint(o, Vector3.right, Vector3.forward, a);
                var pb = BotVehicleBoarding.ExitPoint(o, Vector3.right, Vector3.forward, b);
                Assert.Greater((pa - pb).magnitude, 0.5f, "koltuk " + a + " / " + b);
            }
        }
    }
}
#endif
