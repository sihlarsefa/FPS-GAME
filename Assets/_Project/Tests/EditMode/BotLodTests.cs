#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.AI;

namespace Project.Tests
{
    public class BotLodTests
    {
        [Test]
        public void Tier_ByDistance()
        {
            Assert.AreEqual(0, BotLod.ComputeTier(50f, 0));
            Assert.AreEqual(1, BotLod.ComputeTier(150f, 0));
            Assert.AreEqual(2, BotLod.ComputeTier(300f, 0));
        }

        [Test]
        public void Tier_HasHysteresis()
        {
            Assert.AreEqual(0, BotLod.ComputeTier(125f, 0));
            Assert.AreEqual(1, BotLod.ComputeTier(115f, 1));
            Assert.AreEqual(0, BotLod.ComputeTier(105f, 1));
            Assert.AreEqual(2, BotLod.ComputeTier(245f, 2));
        }
    }
}
#endif
