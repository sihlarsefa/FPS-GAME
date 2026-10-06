using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CosmeticsServiceTests
    {
        private sealed class MemStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public bool HasKey(string key) => Ints.ContainsKey(key);
            public float GetFloat(string key, float fallback) => fallback;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) { }
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Save() { }
        }

        private static List<CosmeticDefinition> Defs() => new List<CosmeticDefinition>
        {
            new CosmeticDefinition { id = "camo_standard", slot = "camo", unlockMethod = "default" },
            new CosmeticDefinition { id = "camo_forest", slot = "camo", unlockXp = 5000, unlockMethod = "career_xp" },
            new CosmeticDefinition { id = "camo_coast", slot = "camo", unlockMethod = "season_track" },
            new CosmeticDefinition { id = "beret_green", slot = "beret", unlockMethod = "default" },
        };

        [Test]
        public void DefaultsOwned_OthersLocked()
        {
            var s = new CosmeticsService(new MemStore(), Defs());
            Assert.IsTrue(s.IsOwned("camo_standard"));
            Assert.IsFalse(s.IsOwned("camo_forest"));
            Assert.AreEqual("camo_standard", s.GetEquipped("camo"));
        }

        [Test]
        public void XpUnlocksCareerItemsOnly()
        {
            var s = new CosmeticsService(new MemStore(), Defs());
            Assert.AreEqual(1, s.SyncUnlocks(6000));
            Assert.IsTrue(s.IsOwned("camo_forest"));
            Assert.IsFalse(s.IsOwned("camo_coast"));
        }

        [Test]
        public void EquipRequiresOwnership_AndPersists()
        {
            var store = new MemStore();
            var s = new CosmeticsService(store, Defs());
            Assert.IsFalse(s.Equip("camo_forest"));
            s.SyncUnlocks(5000);
            Assert.IsTrue(s.Equip("camo_forest"));

            var s2 = new CosmeticsService(store, Defs());
            Assert.AreEqual("camo_forest", s2.GetEquipped("camo"));
            Assert.IsTrue(s2.IsOwned("camo_forest"));
        }

        [Test]
        public void GrantUnlocksSeasonItem()
        {
            var s = new CosmeticsService(new MemStore(), Defs());
            Assert.IsTrue(s.Grant("camo_coast"));
            Assert.IsFalse(s.Grant("camo_coast"));
            Assert.IsFalse(s.Grant("nope"));
            Assert.IsTrue(s.Equip("camo_coast"));
        }

        [Test]
        public void NullStoreAndDefinitionsAreSafe()
        {
            var s = new CosmeticsService(null, null);
            Assert.AreEqual(0, s.Items.Count);
            Assert.IsNull(s.GetEquipped("camo"));
        }
    }
}
