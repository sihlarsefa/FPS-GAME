using NUnit.Framework;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    public sealed class KillChainRulesTests
    {
        [Test]
        public void Chain_ExtendsWithinWindow_ResetsAfter()
        {
            var r = new KillChainRules();
            Assert.AreEqual(1, r.RegisterKill(10f));
            Assert.AreEqual(2, r.RegisterKill(17.9f));
            Assert.AreEqual(3, r.RegisterKill(25.8f));
            Assert.AreEqual(1, r.RegisterKill(40f));
        }

        [Test]
        public void Titles_Escalate()
        {
            Assert.IsNull(KillChainRules.ChainTitle(1));
            Assert.AreEqual("ÇİFTE", KillChainRules.ChainTitle(2));
            Assert.AreEqual("ÜÇLÜ", KillChainRules.ChainTitle(3));
            Assert.AreEqual("SERİ HAREKÂT", KillChainRules.ChainTitle(7));
            Assert.Greater(KillChainRules.FontSize(4), KillChainRules.FontSize(1));
        }

        [Test]
        public void Headline_Variants()
        {
            Assert.AreEqual("LEŞ — Ali  [G3]", KillChainRules.Headline(1, false, "Ali", "G3"));
            StringAssert.StartsWith("KAFADAN", KillChainRules.Headline(1, true, "Ali", null));
            StringAssert.StartsWith("ÇİFTE", KillChainRules.Headline(2, true, "Ali", null));
        }

        [Test]
        public void Reset_ClearsChain()
        {
            var r = new KillChainRules();
            r.RegisterKill(1f); r.RegisterKill(2f);
            r.Reset();
            Assert.AreEqual(1, r.RegisterKill(3f));
        }
    }
}
