using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.World.Lighting;

namespace Project.Tests.EditMode
{
    public sealed class PoiLightScheduleTests
    {
        [Test]
        public void GunduzKapali_GeceAcik()
        {
            Assert.IsFalse(PoiLights.IsActiveAt(TimeOfDay.Gunduz));
            Assert.IsTrue(PoiLights.IsActiveAt(TimeOfDay.Gece));
            Assert.IsTrue(PoiLights.IsActiveAt(TimeOfDay.Safak));
            Assert.IsTrue(PoiLights.IsActiveAt(TimeOfDay.Aksam));
        }
    }
}
