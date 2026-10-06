using NUnit.Framework;
using Project.Infrastructure.Characters.Animation;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class WearyReactionRulesTests
    {
        [Test]
        public void Closeness_Range()
        {
            Assert.AreEqual(1f, WearyReactionRules.Closeness(0f), 0.001f);
            Assert.AreEqual(0f, WearyReactionRules.Closeness(3f), 0.001f);
            Assert.AreEqual(0.5f, WearyReactionRules.Closeness(0.75f), 0.001f);
        }

        [Test]
        public void Limp_OnlyLowHealthAndWalking()
        {
            Assert.AreEqual(0f, WearyReactionRules.LimpWeight(0.8f, 1f), 0.001f);
            Assert.AreEqual(0f, WearyReactionRules.LimpWeight(0.2f, 0f), 0.001f);
            Assert.IsTrue(WearyReactionRules.LimpWeight(0.2f, 1f) > 0.4f);
            Assert.AreEqual(0f, WearyReactionRules.LimpWeight(0f, 1f), 0.001f);
        }

        [Test]
        public void Side_IsSigned()
        {
            var a = WearyReactionRules.SideOf(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 5f));
            var b = WearyReactionRules.SideOf(Vector3.zero, Vector3.forward, new Vector3(-1f, 0f, 5f));
            Assert.AreEqual(-a, b, 0.001f);
        }
    }
}
