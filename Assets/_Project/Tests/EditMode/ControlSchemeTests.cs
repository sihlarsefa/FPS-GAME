using System.Text;
using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class ControlSchemeTests
    {
        [Test]
        public void DefaultScheme_HasNoConflictsInOverlappingContexts()
        {
            var conflicts = ControlScheme.FindConflicts(ControlScheme.BuildEntries(new InputBindingMap()));
            var sb = new StringBuilder();
            foreach (var (a, b) in conflicts)
                sb.Append(a.Input).Append(": ").Append(a.Label).Append(" <-> ").Append(b.Label).Append('\n');
            Assert.AreEqual(0, conflicts.Count, sb.ToString());
        }

        [Test]
        public void Detects_SameKeyInOverlappingContexts()
        {
            var entries = new[]
            {
                new ControlEntry(ControlContext.OnFoot, "F3", "Emir", false),
                new ControlEntry(ControlContext.Always, "F3", "Perf", false),
            };
            Assert.AreEqual(1, ControlScheme.FindConflicts(entries).Count);
        }

        [Test]
        public void SameKeyInDisjointContexts_IsAllowed()
        {
            var entries = new[]
            {
                new ControlEntry(ControlContext.OnFoot, "Q", "Sola eğil", true),
                new ControlEntry(ControlContext.Spectator, "Q", "Önceki hedef", false),
            };
            Assert.AreEqual(0, ControlScheme.FindConflicts(entries).Count);
        }

        [Test]
        public void AliasesOfSameAction_AreNotConflicts()
        {
            var entries = new[]
            {
                new ControlEntry(ControlContext.OnFoot, "Tab", "Envanter", true),
                new ControlEntry(ControlContext.OnFoot, "Tab", "Envanter", true),
            };
            Assert.AreEqual(0, ControlScheme.FindConflicts(entries).Count);
        }

        [Test]
        public void DefaultBindings_AvoidReservedKeys()
        {
            foreach (var a in InputBindingMap.All)
                foreach (var k in InputBindingMap.DefaultKeys(a))
                    Assert.IsFalse(ControlScheme.IsReserved(k), a + " varsayılanı ayrılmış tuş: " + k);
        }

        [Test]
        public void ReservedKeys_IncludeSystemKeys()
        {
            foreach (var k in new[] { "Escape", "F3", "F10", "Backquote", "CapsLock", "Y", "U", "V" })
                Assert.IsTrue(ControlScheme.IsReserved(k), k);
            Assert.IsFalse(ControlScheme.IsReserved("F"));
            Assert.IsFalse(ControlScheme.IsReserved(null));
        }

        [Test]
        public void EveryBindAction_HasSomeContext()
        {
            foreach (var a in InputBindingMap.All)
                Assert.AreNotEqual(ControlContext.None, ControlScheme.ContextOf(a), a.ToString());
        }

        [Test]
        public void InteractAndSeatKeys_ShareNoKeyWithWeaponSlotsExceptByDesign()
        {
            // Araçta 1/2 koltuk değiştirir (Slot1/Slot2 ile aynı eylem); F etkileşimi hiçbir slotla çakışmaz.
            var m = new InputBindingMap();
            Assert.IsFalse(m.Has(BindAction.Interact, "Digit1"));
            Assert.IsTrue(ControlScheme.ContextOf(BindAction.Slot1).HasFlag(ControlContext.Driver));
        }
    }
}
