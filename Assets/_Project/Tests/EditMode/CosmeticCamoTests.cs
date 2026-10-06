using NUnit.Framework;
using Project.Application.Services;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CosmeticCamoTests
    {
        [Test]
        public void CamoCatalog_HasEightUniqueDefs_WithWraps()
        {
            Assert.AreEqual(8, CamoCatalog.All.Length);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var d in CamoCatalog.All)
            {
                Assert.IsTrue(ids.Add(d.Id));
                Assert.AreEqual(4, d.Palette.Length);
                Assert.IsTrue(CamoCatalog.TryGetByWrap(d.WrapId, out var back));
                Assert.AreEqual(d.Id, back.Id);
            }
        }

        [Test]
        public void EveryPattern_IsDeterministic_AndHasVariation()
        {
            foreach (var d in CamoCatalog.All)
            {
                var p = d.Palette;
                var a = CamoPatternPixels.Generate(d.Pattern, p[0], p[1], p[2], p[3], 5, 64);
                var b = CamoPatternPixels.Generate(d.Pattern, p[0], p[1], p[2], p[3], 5, 64);
                Assert.AreEqual(64 * 64, a.Length);
                var distinct = new System.Collections.Generic.HashSet<int>();
                for (var i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].r, b[i].r);
                    Assert.AreEqual(255, a[i].a);
                    distinct.Add(a[i].r << 16 | a[i].g << 8 | a[i].b);
                }

                Assert.GreaterOrEqual(distinct.Count, 2, d.Id);
            }
        }

        [Test]
        public void ApplyCosmetic_CamoSetsPatternAndPalette()
        {
            var look = SoldierLook.Default;
            Assert.IsTrue(look.ApplyCosmetic("camo_steppe"));
            Assert.AreEqual(CamoPatternKind.Steppe, look.CamoPattern);
            Assert.IsFalse(look.ApplyCosmetic("unknown_id"));
            Assert.AreEqual(CamoPatternKind.Steppe, look.CamoPattern);
        }

        [Test]
        public void Ghillie_OnlyForSniper()
        {
            var look = SoldierLook.Default;
            look.ApplyCosmetic("ghillie_snow", false);
            Assert.AreEqual(GhillieKind.None, look.Ghillie);
            look.ApplyCosmetic("ghillie_snow", true);
            Assert.AreEqual(GhillieKind.Snow, look.Ghillie);
            Assert.AreEqual(SoldierLook.Default.Ghillie, GhillieKind.None);
            Assert.AreEqual(GhillieKind.Snow, look.Clone().Ghillie);
        }

        [Test]
        public void GhillieBits_Deterministic()
        {
            var a = GhillieBits.Generate(GhillieKind.Woodland, 20, 3);
            var b = GhillieBits.Generate(GhillieKind.Woodland, 20, 3);
            Assert.AreEqual(20, a.Length);
            Assert.AreEqual(a[7].U, b[7].U);
            Assert.AreEqual(0, GhillieBits.Generate(GhillieKind.None, 20, 3).Length);
        }

        [Test]
        public void FacePaint_NoneIsTransparent_OthersPaintSomething()
        {
            foreach (FacePaintKind k in System.Enum.GetValues(typeof(FacePaintKind)))
            {
                var px = CamoPatternPixels.FacePaintPixels(k, 64);
                var painted = 0;
                foreach (var c in px)
                    if (c.a > 0) painted++;
                if (k == FacePaintKind.None) Assert.AreEqual(0, painted);
                else Assert.Greater(painted, 20, k.ToString());
            }
        }

        [Test]
        public void SeasonRef_Parse_And_Rarity_And_Grant()
        {
            Assert.IsTrue(CosmeticsService.TryParseSeasonRef("s1:premium:t41", out var s, out var tr, out var t));
            Assert.AreEqual(1, s); Assert.AreEqual("premium", tr); Assert.AreEqual(41, t);
            Assert.IsFalse(CosmeticsService.TryParseSeasonRef("s1:vip:t4", out _, out _, out _));
            Assert.IsFalse(CosmeticsService.TryParseSeasonRef("garbage", out _, out _, out _));
            Assert.AreEqual("legendary", CosmeticsService.RarityOf(new CosmeticDefinition { rarity = "legendary", unlockMethod = "default" }));
            Assert.AreEqual("rare", CosmeticsService.RarityOf(new CosmeticDefinition { unlockMethod = "career_xp", unlockXp = 3000 }));
            Assert.IsTrue(CosmeticsService.AllowedForRole(new CosmeticDefinition { roleRequired = "sniper" }, "sniper"));
            Assert.IsFalse(CosmeticsService.AllowedForRole(new CosmeticDefinition { roleRequired = "sniper" }, "medic"));

            var defs = new[]
            {
                new CosmeticDefinition { id = "a", slot = "camo", unlockMethod = "season_track", seasonRef = "s1:premium:t10" },
                new CosmeticDefinition { id = "b", slot = "camo", unlockMethod = "season_track", seasonRef = "s1:premium:t30" },
                new CosmeticDefinition { id = "c", slot = "camo", unlockMethod = "season_track", seasonRef = "s1:free:t5" }
            };
            var svc = new CosmeticsService(null, defs);
            Assert.AreEqual(1, svc.GrantSeasonTier(1, "premium", 20));
            Assert.IsTrue(svc.IsOwned("a"));
            Assert.IsFalse(svc.IsOwned("b"));
            Assert.IsFalse(svc.IsOwned("c"));
            StringAssert.Contains("kademe 30", CosmeticsService.UnlockRuleText(defs[1]));
        }
    }
}
