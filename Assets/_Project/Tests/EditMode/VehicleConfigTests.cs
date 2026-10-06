#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vehicles;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class VehicleConfigTests
    {
        [Test]
        public void Kirpi_KeepsOriginalValues()
        {
            var k = VehicleConfig.Kirpi;
            Assert.AreEqual(1200f, k.MaxHealth);
            Assert.AreEqual(85f, k.MaxSpeedKmh);
            Assert.AreEqual(9, k.SeatCount);
        }

        [Test]
        public void Cobra_FasterLighterFewerSeats()
        {
            var c = VehicleConfig.Cobra;
            Assert.Greater(c.MaxSpeedKmh, VehicleConfig.Kirpi.MaxSpeedKmh);
            Assert.Less(c.MaxHealth, VehicleConfig.Kirpi.MaxHealth);
            Assert.AreEqual(6, c.SeatCount);
        }

        [Test]
        public void PickForLocation_Deterministic()
        {
            var p = new Vector3(310f, 0f, -120f);
            Assert.AreEqual(VehicleConfig.PickForLocation(p, 7), VehicleConfig.PickForLocation(p, 7));
        }

        [Test]
        public void PickForLocation_MixesBothKinds()
        {
            var cobra = 0;
            for (var i = 0; i < 100; i++)
                if (VehicleConfig.PickForLocation(new Vector3(300f + i * 7f, 0f, 40f + i * 3f), 42) == VehicleKind.Cobra)
                    cobra++;
            Assert.Greater(cobra, 10);
            Assert.Less(cobra, 95);
        }

        [Test]
        public void Hud_UsesName()
            => StringAssert.StartsWith("Otokar Cobra", KirpiCrewRules.Hud(10f, 500f, 750f, false, 0, false, "Otokar Cobra"));
    }
}
#endif
