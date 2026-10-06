using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
#if UNITY_EDITOR
using Project.Infrastructure.Weapons;
#endif

namespace Project.Tests.EditMode
{
    /// <summary>silahlar2: yeni Türk silahlarının katalog, eşya, ganimet ve model tutarlılığı.</summary>
    [TestFixture]
    public sealed class Silahlar2Tests
    {
        private static readonly string[] NewIds =
        {
            WeaponIds.Sar223, WeaponIds.Mpt76K, WeaponIds.Mete, WeaponIds.Sar762Mt, WeaponIds.Mg3, WeaponIds.EscortMagnum
        };

        [Test]
        public void NewWeapons_AreInCatalog_WithUniqueIds()
        {
            var seen = new HashSet<string>();
            foreach (var id in NewIds)
            {
                Assert.IsTrue(seen.Add(id), id);
                Assert.IsTrue(WeaponCatalog.TryGet(id, out var def), id);
                Assert.AreEqual(id, def.WeaponId);
                Assert.IsNotEmpty(def.DisplayName);
                Assert.AreNotEqual(id, def.DisplayName);
                Assert.Greater(def.Damage, 0f, id);
                Assert.Greater(def.MagazineSize, 0, id);
                Assert.Greater(def.FireIntervalSeconds, 0f, id);
                Assert.Greater(def.ReloadDurationSeconds, 0f, id);
                Assert.AreNotEqual(AmmoType.None, def.AmmoType, id);
                Assert.GreaterOrEqual(def.FalloffEnd, def.FalloffStart, id);
                Assert.IsTrue(def.FireModes.Length > 0, id);
            }
        }

        [Test]
        public void NewWeapons_ExistingIdsStayStable()
        {
            Assert.AreEqual("pistol_sar9", WeaponIds.Sar9);
            Assert.AreEqual("sg_escort", WeaponIds.Escort);
            Assert.AreEqual("lmg_pmt76", WeaponIds.Pmt76);
        }

        [Test]
        public void NewWeapons_HaveItemsMatchingAmmo()
        {
            foreach (var id in NewIds)
            {
                Assert.IsTrue(ItemCatalog.IsWeapon(id), id);
                Assert.AreEqual(WeaponCatalog.Get(id).AmmoType, ItemCatalog.Get(id).AmmoType, id);
            }
        }

        [Test]
        public void NewWeapons_RpmAndRoles()
        {
            Assert.GreaterOrEqual(WeaponCatalog.RoundsPerMinute(WeaponCatalog.Get(WeaponIds.Mg3)), 1000f);
            Assert.Greater(WeaponCatalog.RoundsPerMinute(WeaponCatalog.Get(WeaponIds.Mg3)),
                WeaponCatalog.RoundsPerMinute(WeaponCatalog.Get(WeaponIds.Pmt76)));
            Assert.AreEqual(WeaponCategory.Pistol, WeaponCatalog.Get(WeaponIds.Mete).Category);
            Assert.AreEqual(AmmoType.Mm556, WeaponCatalog.Get(WeaponIds.Sar223).AmmoType);
            Assert.AreEqual(AmmoType.Mm762, WeaponCatalog.Get(WeaponIds.Mpt76K).AmmoType);
            Assert.AreEqual(WeaponCategory.Dmr, WeaponCatalog.Get(WeaponIds.Sar762Mt).Category);
            Assert.AreEqual(AmmoType.Gauge12, WeaponCatalog.Get(WeaponIds.EscortMagnum).AmmoType);
            Assert.Greater(WeaponCatalog.Get(WeaponIds.EscortMagnum).PelletCount, 1);
        }

        [Test]
        public void NewWeapons_BalanceStaysBelowExistingCeiling()
        {
            // Saniyelik hasar tavanı: hiçbir yeni silah mevcut en yükseğin 1,6 katını aşmamalı.
            var ceiling = 0f;
            foreach (var w in WeaponCatalog.All)
            {
                if (w.Category == WeaponCategory.Sniper || System.Array.IndexOf(NewIds, w.WeaponId) >= 0)
                    continue;
                ceiling = System.Math.Max(ceiling, Dps(w));
            }

            foreach (var id in NewIds)
                Assert.Less(Dps(WeaponCatalog.Get(id)), ceiling * 1.6f, id);
        }

        private static float Dps(WeaponDefinitionData w) =>
            w.Damage * w.PelletCount / w.FireIntervalSeconds;

        [Test]
        public void NewWeapons_AppearInLootTables()
        {
            var service = new LootSpawnService();
            foreach (var id in NewIds)
            {
                var any = false;
                foreach (LootTier tier in System.Enum.GetValues(typeof(LootTier)))
                    any |= service.GetWeight(tier, id) > 0f;
                Assert.IsTrue(any, id);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void NewWeapons_ResolveToDistinctModelStyles()
        {
            var styles = new HashSet<WeaponStyle>();
            foreach (var id in NewIds)
            {
                var style = WeaponStyles.Resolve(WeaponCatalog.Get(id));
                Assert.AreNotEqual(WeaponStyle.None, style, id);
                Assert.IsTrue(styles.Add(style), id);
            }

            Assert.IsTrue(WeaponStyles.IsPistol(WeaponStyle.MeteSft));
            Assert.IsTrue(WeaponStyles.IsBeltFed(WeaponStyle.Mg3));
            Assert.IsFalse(WeaponStyles.IsPumpAction(WeaponStyle.EscortMagnum));
        }
#endif
    }
}
