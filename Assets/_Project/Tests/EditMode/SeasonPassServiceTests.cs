using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class SeasonPassServiceTests
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

        private static SeasonPassDefinition Def() => new SeasonPassDefinition
        {
            maxTier = 50,
            xpPerTier = 600,
            free = new List<SeasonRewardEntry>
            {
                new SeasonRewardEntry { tier = 1, cosmeticId = "camo_coast" },
                new SeasonRewardEntry { tier = 3, cosmeticId = "beret_steel" }
            },
            premium = new List<SeasonRewardEntry> { new SeasonRewardEntry { tier = 2, cosmeticId = "camo_night" } }
        };

        private static CosmeticsService Cos(MemStore st) => new CosmeticsService(st, new List<CosmeticDefinition>
        {
            new CosmeticDefinition { id = "camo_coast", slot = "camo", unlockMethod = "season_track" },
            new CosmeticDefinition { id = "beret_steel", slot = "beret", unlockMethod = "season_track" },
            new CosmeticDefinition { id = "camo_night", slot = "camo", unlockMethod = "season_track" }
        });

        private static MatchResult R(bool win, int place, int kills) =>
            new MatchResult(win, place, 10, kills, 0, 0f, 0f, 0f, null);

        [Test]
        public void MatchXp_RewardsWinnerAndKills_AndIsCapped()
        {
            Assert.Greater(SeasonPassService.XpForMatch(R(true, 1, 3)), SeasonPassService.XpForMatch(R(false, 9, 3)));
            Assert.Greater(SeasonPassService.XpForMatch(R(false, 20, 5)), SeasonPassService.XpForMatch(R(false, 20, 0)));
            Assert.AreEqual(SeasonPassService.MaxXpPerMatch, SeasonPassService.XpForMatch(R(true, 1, 500)));
            Assert.AreEqual(100, SeasonPassService.XpForMatch(R(false, 20, 0)));
        }

        [Test]
        public void Tiers_ClampAndProgress()
        {
            var s = new SeasonPassService(new MemStore(), null, Def());
            Assert.AreEqual(0, s.CurrentTier);
            s.AddXp(1300);
            Assert.AreEqual(2, s.CurrentTier);
            Assert.AreEqual(100, s.XpIntoTier);
            s.AddXp(10_000_000);
            Assert.AreEqual(50, s.CurrentTier);
            Assert.AreEqual(1f, s.TierProgress);
            Assert.AreEqual(30000, s.Xp);
            Assert.AreEqual(0, s.AddXp(-5));
        }

        [Test]
        public void Claim_RequiresTier_GrantsCosmetic_OnlyOnce()
        {
            var st = new MemStore();
            var cos = Cos(st);
            var s = new SeasonPassService(st, cos, Def());
            Assert.IsFalse(s.Claim(1));
            s.AddXp(600);
            Assert.IsTrue(s.CanClaim(1));
            Assert.IsTrue(s.Claim(1));
            Assert.IsTrue(cos.IsOwned("camo_coast"));
            Assert.IsFalse(s.Claim(1));
            Assert.IsFalse(s.CanClaim(2)); // ücretsiz ödül yok
        }

        [Test]
        public void ClaimAll_AndPersistence()
        {
            var st = new MemStore();
            var s = new SeasonPassService(st, Cos(st), Def());
            s.AddXp(1800);
            Assert.AreEqual(2, s.ClaimableCount());
            Assert.AreEqual(2, s.ClaimAll());

            var s2 = new SeasonPassService(st, Cos(st), Def());
            Assert.AreEqual(1800, s2.Xp);
            Assert.IsTrue(s2.IsClaimed(1) && s2.IsClaimed(3));
            Assert.AreEqual(0, s2.ClaimableCount());
        }

        [Test]
        public void Premium_IsComingSoon_NeverClaimable()
        {
            var st = new MemStore();
            var cos = Cos(st);
            var s = new SeasonPassService(st, cos, Def());
            s.AddXp(30000);
            Assert.IsFalse(SeasonPassService.PremiumAvailable);
            Assert.IsFalse(s.ClaimPremium(2));
            Assert.IsFalse(cos.IsOwned("camo_night"));
        }

        [Test]
        public void NullSafe()
        {
            var s = new SeasonPassService(null, null, null);
            Assert.AreEqual(0, s.AddXp(0));
            s.AddXp(700);
            Assert.AreEqual(1, s.CurrentTier);
            Assert.IsFalse(s.Claim(1));
        }

#if UNITY_EDITOR
        [System.Serializable]
        private sealed class JsonRoot { public int maxTier; public int xpPerTier; public List<SeasonRewardEntry> free; public List<SeasonRewardEntry> premium; }

        [System.Serializable]
        private sealed class CosRoot { public List<CosmeticDefinition> items; }

        [Test]
        public void ShippedJson_RewardsReferenceExistingCosmetics()
        {
            var pass = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Progression/season1_pass");
            var cos = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Progression/cosmetics");
            Assert.IsNotNull(pass);
            Assert.IsNotNull(cos);
            var p = UnityEngine.JsonUtility.FromJson<JsonRoot>(pass.text);
            var ids = new HashSet<string>();
            foreach (var d in UnityEngine.JsonUtility.FromJson<CosRoot>(cos.text).items) ids.Add(d.id);
            Assert.AreEqual(50, p.maxTier);
            foreach (var e in p.free) { Assert.IsTrue(ids.Contains(e.cosmeticId), e.cosmeticId); Assert.IsTrue(e.tier >= 1 && e.tier <= 50); }
            foreach (var e in p.premium) { Assert.IsTrue(ids.Contains(e.cosmeticId), e.cosmeticId); Assert.IsTrue(e.tier >= 1 && e.tier <= 50); }
        }
#endif
    }
}
