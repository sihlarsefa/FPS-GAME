using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Tests
{
    public sealed class ThrowableRulesTests
    {
        [Test]
        public void FlashIntensity_FacingCloseIsStrong_BehindIsWeak()
        {
            var front = ThrowableRules.FlashIntensity(0f, 3f, ThrowableRules.FlashRadius, true);
            var behind = ThrowableRules.FlashIntensity(180f, 3f, ThrowableRules.FlashRadius, true);
            Assert.Greater(front, 0.9f);
            Assert.Less(behind, front * 0.25f);
        }

        [Test]
        public void FlashIntensity_DecreasesWithDistance_AndZeroWithoutLosOrBeyondRadius()
        {
            var near = ThrowableRules.FlashIntensity(10f, 4f, 16f, true);
            var far = ThrowableRules.FlashIntensity(10f, 12f, 16f, true);
            Assert.Greater(near, far);
            Assert.AreEqual(0f, ThrowableRules.FlashIntensity(0f, 4f, 16f, false));
            Assert.AreEqual(0f, ThrowableRules.FlashIntensity(0f, 16f, 16f, true));
        }

        [Test]
        public void FlashAlpha_HoldsThenFadesToZero()
        {
            var blind = ThrowableRules.FlashBlindSeconds(1f);
            Assert.AreEqual(ThrowableRules.FlashMaxBlindSeconds, blind, 0.01f);
            var hold = ThrowableRules.FlashAlpha(blind * 0.2f, blind, 1f);
            var fading = ThrowableRules.FlashAlpha(blind * 0.7f, blind, 1f);
            Assert.Greater(hold, fading);
            Assert.AreEqual(0f, ThrowableRules.FlashAlpha(blind, blind, 1f));
            Assert.AreEqual(0f, ThrowableRules.FlashBlindSeconds(0f));
        }

        [Test]
        public void FireRadius_GrowsOnSlope_Capped()
        {
            var flat = ThrowableRules.FireRadiusOnSlope(4.5f, 0f);
            var steep = ThrowableRules.FireRadiusOnSlope(4.5f, 40f);
            var extreme = ThrowableRules.FireRadiusOnSlope(4.5f, 85f);
            Assert.AreEqual(4.5f, flat, 1e-4f);
            Assert.Greater(steep, flat);
            Assert.AreEqual(4.5f * (1f + ThrowableRules.FireMaxSlopeSpread), extreme, 1e-3f);
        }

        [Test]
        public void FireTickDamage_CenterFullEdgeHalf_OutsideZero()
        {
            Assert.AreEqual(ThrowableRules.FireTickDamage, ThrowableRules.FireTickDamageAt(0f, 4.5f), 1e-4f);
            Assert.AreEqual(ThrowableRules.FireTickDamage * ThrowableRules.FireEdgeDamageFactor,
                ThrowableRules.FireTickDamageAt(4.5f, 4.5f), 1e-4f);
            Assert.AreEqual(0f, ThrowableRules.FireTickDamageAt(5f, 4.5f));
        }

        [Test]
        public void BurnTicks_RefreshInsideAndDecayOutside()
        {
            var ticks = ThrowableRules.BurnTicksLeft(true, 0);
            Assert.AreEqual(ThrowableRules.FireBurnTicksAfterLeaving, ticks);
            ticks = ThrowableRules.BurnTicksLeft(false, ticks);
            ticks = ThrowableRules.BurnTicksLeft(false, ticks);
            ticks = ThrowableRules.BurnTicksLeft(false, ticks);
            Assert.AreEqual(0, ticks);
            Assert.AreEqual(0, ThrowableRules.BurnTicksLeft(false, 0));
        }

        [Test]
        public void Rain_AcceleratesDecay_AndHeavyRainExtinguishes()
        {
            Assert.AreEqual(1f, ThrowableRules.FireDecayRate(0f), 1e-4f);
            Assert.AreEqual(5f, ThrowableRules.FireDecayRate(1f), 1e-4f);
            Assert.IsFalse(ThrowableRules.RainExtinguishes(0.3f));
            Assert.IsTrue(ThrowableRules.RainExtinguishes(0.9f));
        }

        [Test]
        public void DecoyTiming_BurstsThenGap()
        {
            Assert.AreEqual(ThrowableRules.DecoyShotSpacing, ThrowableRules.DecoyNextDelay(0, 4, 0.5f), 1e-4f);
            var gap = ThrowableRules.DecoyNextDelay(3, 4, 0.5f);
            Assert.GreaterOrEqual(gap, 0.6f);
            Assert.LessOrEqual(gap, 2.2f);
            for (var i = 0; i <= 10; i++)
            {
                var n = ThrowableRules.DecoyBurstLength(i / 10f);
                Assert.GreaterOrEqual(n, 3);
                Assert.LessOrEqual(n, 6);
            }
        }

        [Test]
        public void DefaultFuse_AndItemIds_AreDistinctPerKind()
        {
            Assert.AreEqual(ThrowableProjectile.FragFuseSeconds, ThrowableRules.DefaultFuse(ThrowableKind.Frag));
            Assert.AreEqual(ThrowableRules.FlashFuseSeconds, ThrowableRules.DefaultFuse(ThrowableKind.Flash));
            Assert.AreEqual(ItemIds.MolotovGrenade, ThrowableRules.ItemIdFor(ThrowableKind.Molotov));
            Assert.NotNull(ItemCatalog.Get(ItemIds.FlashGrenade));
            Assert.NotNull(ItemCatalog.Get(ItemIds.DecoyGrenade));
            // Ağ indeksleri: yeni kimlikler katalog sonunda.
            var all = ItemCatalog.All;
            Assert.AreEqual(ItemIds.DecoyGrenade, all[all.Count - 1].Id);
        }

        [Test]
        public void SimulateArc_RisesThenFalls_AndAdvancesForward()
        {
            var pts = new List<Vector3>();
            ThrowableRules.SimulateArc(Vector3.zero, new Vector3(0f, 3.2f, 17f), 0.045f, 90, pts);
            Assert.AreEqual(91, pts.Count);
            var maxY = 0f;
            for (var i = 0; i < pts.Count; i++)
                maxY = Mathf.Max(maxY, pts[i].y);
            Assert.Greater(maxY, 0.3f);
            Assert.Less(pts[pts.Count - 1].y, maxY);
            Assert.Greater(pts[pts.Count - 1].z, 30f);
        }
    }
}
