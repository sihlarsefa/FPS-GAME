#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vehicles;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class KirpiCrewRulesTests
    {
        [Test]
        public void FirstFreeSeat_PicksLowestFree()
        {
            Assert.AreEqual(2, KirpiCrewRules.FirstFreeSeat(new[] { true, true, false, false }));
            Assert.AreEqual(-1, KirpiCrewRules.FirstFreeSeat(new[] { true, true }));
            Assert.AreEqual(-1, KirpiCrewRules.FirstFreeSeat(null));
        }

        [Test]
        public void DisembarkOnStop_RequiresMovementFirst()
        {
            Assert.IsFalse(KirpiCrewRules.ShouldDisembarkOnStop(false, 10f));
            Assert.IsFalse(KirpiCrewRules.ShouldDisembarkOnStop(true, 1f));
            Assert.IsTrue(KirpiCrewRules.ShouldDisembarkOnStop(true, 3f));
        }

        [Test]
        public void DisembarkOnOrder_OnlyForNonFollow()
        {
            Assert.IsFalse(KirpiCrewRules.ShouldDisembarkOnOrder(true));
            Assert.IsTrue(KirpiCrewRules.ShouldDisembarkOnOrder(false));
        }

        [Test]
        public void Hud_ContainsSpeedAndArmor()
        {
            var s = KirpiCrewRules.Hud(54.7f, 600f, 1200f, true, 120, false);
            StringAssert.Contains("54 km/sa", s);
            StringAssert.Contains("%50", s);
            StringAssert.Contains("MG 120", s);
        }
    }
}
#endif
