using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class InputBindingMapTests
    {
        [Test]
        public void Defaults_MatchLegacyKeys()
        {
            var m = new InputBindingMap();
            Assert.IsTrue(m.Has(BindAction.Jump, "Space"));
            Assert.IsTrue(m.Has(BindAction.Inventory, "Tab"));
            Assert.IsTrue(m.Has(BindAction.Inventory, "I"));
        }

        [Test]
        public void Inspect_DefaultsToK()
        {
            var m = new InputBindingMap();
            Assert.IsTrue(m.Has(BindAction.Inspect, "K"));
        }

        [Test]
        public void Set_StealsKeyFromOtherAction()
        {
            var m = new InputBindingMap();
            m.Set(BindAction.Jump, "R");
            Assert.IsTrue(m.Has(BindAction.Jump, "R"));
            Assert.IsFalse(m.Has(BindAction.Reload, "R"));
            Assert.AreEqual(BindAction.Jump, m.FindOwner("R"));
        }

        [Test]
        public void SerializeRoundTrip_AndReset()
        {
            var m = new InputBindingMap();
            m.Set(BindAction.Map, "N");
            var r = InputBindingMap.Deserialize(m.Serialize());
            Assert.IsTrue(r.Has(BindAction.Map, "N"));
            r.ResetToDefaults();
            Assert.IsTrue(r.Has(BindAction.Map, "M"));
        }

        [Test]
        public void Deserialize_GarbageFallsBackToDefaults()
        {
            var m = InputBindingMap.Deserialize("zzz\nJump=\nNope=Q\n=\n");
            Assert.IsTrue(m.Has(BindAction.Jump, "Space"));
            Assert.IsNotNull(InputBindingMap.Deserialize(null));
        }
    }
}
