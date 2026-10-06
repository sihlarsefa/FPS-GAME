using System;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    /// <summary>P2 silah dengesi: zırh sınıfı vs kalibre, TTK hedefleri, balistik, elleme süreleri, desen sekmesi, eklenti ödünleşimleri.</summary>
    [TestFixture]
    public sealed class SilahDengeTests
    {
        private sealed class NullBus : IEventBus
        {
            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent { }
            public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
        }

        private static WeaponDefinitionData W(string id) => WeaponCatalog.Get(id);

        // ---------------------------------------------------------------- Kimlik/indeks kararlılığı

        [Test]
        public void Catalog_OrderAndIdsAreStable()
        {
            var expected = new[]
            {
                WeaponIds.Sar9, WeaponIds.Tp9, WeaponIds.Sar109, WeaponIds.Mpt55, WeaponIds.Mpt76, WeaponIds.G3,
                WeaponIds.Knt76, WeaponIds.Jng90, WeaponIds.Pmt76, WeaponIds.Escort, WeaponIds.Sar223, WeaponIds.Mpt76K,
                WeaponIds.Mete, WeaponIds.Sar762Mt, WeaponIds.Mg3, WeaponIds.EscortMagnum
            };
            Assert.AreEqual(expected.Length, WeaponCatalog.All.Count);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], WeaponCatalog.All[i].WeaponId, "ağ indeksi sırası bozulmamalı: " + i);
        }

        // ---------------------------------------------------------------- Zırh sınıfı vs kalibre

        [Test]
        public void ArmorEffectiveness_StrongCaliberBeatsLowArmor()
        {
            Assert.AreEqual(1f, PenetrationRules.ArmorEffectiveness(AmmoType.Mm762, 0), 1e-5f, "bilinmeyen seviye = tam etkinlik");
            for (var level = 1; level <= 3; level++)
            {
                var e9 = PenetrationRules.ArmorEffectiveness(AmmoType.Mm9, level);
                var e556 = PenetrationRules.ArmorEffectiveness(AmmoType.Mm556, level);
                var e762 = PenetrationRules.ArmorEffectiveness(AmmoType.Mm762, level);
                Assert.GreaterOrEqual(e9, e556, "seviye " + level);
                Assert.GreaterOrEqual(e556, e762, "seviye " + level);
                Assert.GreaterOrEqual(e762, PenetrationRules.MinArmorEffectiveness - 1e-5f);
                Assert.LessOrEqual(e9, 1f);
            }

            // Yüksek seviye her kalibreye karşı daha etkin.
            foreach (var ammo in new[] { AmmoType.Mm9, AmmoType.Mm556, AmmoType.Mm762, AmmoType.Gauge12 })
            {
                Assert.GreaterOrEqual(PenetrationRules.ArmorEffectiveness(ammo, 3), PenetrationRules.ArmorEffectiveness(ammo, 2), ammo.ToString());
                Assert.GreaterOrEqual(PenetrationRules.ArmorEffectiveness(ammo, 2), PenetrationRules.ArmorEffectiveness(ammo, 1), ammo.ToString());
            }

            Assert.AreEqual(0.65f, PenetrationRules.ArmorEffectiveness(AmmoType.Mm762, 3), 0.05f);
            Assert.AreEqual(1f, PenetrationRules.ArmorEffectiveness(AmmoType.Gauge12, 3), 0.05f);
        }

        [Test]
        public void ComputeBulletDamage_UsesArmorClassVsCaliber()
        {
            var mpt76 = W(WeaponIds.Mpt76);
            var vest3 = new ArmorPiece("v3", 3, 250f, 0.55f, 250f);
            var r = DamageCalculator.ComputeBulletDamage(mpt76, BodyPart.Torso, 10f, vest3);
            var eff = PenetrationRules.ArmorEffectiveness(AmmoType.Mm762, 3);
            Assert.AreEqual(36f * (1f - 0.55f * eff), r.Damage, 1e-3f);
            Assert.AreEqual(36f * 0.55f * eff, r.ArmorAbsorbed, 1e-3f);

            var vest1 = new ArmorPiece("v1", 1, 200f, 0.30f, 200f);
            var smg = W(WeaponIds.Sar109);
            var smgVsV1 = DamageCalculator.ComputeBulletDamage(smg, BodyPart.Torso, 5f, vest1);
            var rifleVsV1 = DamageCalculator.ComputeBulletDamage(mpt76, BodyPart.Torso, 5f, new ArmorPiece("v1", 1, 200f, 0.30f, 200f));
            Assert.Greater(1f - smgVsV1.Damage / 22f, 1f - rifleVsV1.Damage / 36f, "Sv.1 yelek 9 mm'yi 7.62'den fazla keser");
        }

        [Test]
        public void PredictBulletDamage_DoesNotWearArmor()
        {
            var vest = new ArmorPiece("v2", 2, 220f, 0.40f, 220f);
            var predicted = DamageCalculator.PredictBulletDamage(W(WeaponIds.Mpt55), BodyPart.Torso, 10f, 2, 0.40f);
            Assert.AreEqual(220f, vest.Durability, 1e-5f);
            var real = DamageCalculator.ComputeBulletDamage(W(WeaponIds.Mpt55), BodyPart.Torso, 10f, vest).Damage;
            Assert.AreEqual(real, predicted, 1e-3f);
        }

        // ---------------------------------------------------------------- TTK hedefleri

        [Test]
        public void Ttk_Mpt76_ThreeBodyShotsUnarmoredUnder100m()
        {
            var w = W(WeaponIds.Mpt76);
            Assert.AreEqual(3, DamageCalculator.ShotsToKill(w, BodyPart.Torso, 10f));
            Assert.AreEqual(3, DamageCalculator.ShotsToKill(w, BodyPart.Torso, 99f));
            Assert.AreEqual(4, DamageCalculator.ShotsToKill(w, BodyPart.Torso, 400f));
            Assert.AreEqual(2f * w.FireIntervalSeconds, DamageCalculator.TimeToKill(w, BodyPart.Torso, 50f), 1e-4f);
        }

        [Test]
        public void Ttk_UnarmoredTorsoBandsPerCategory()
        {
            foreach (var w in WeaponCatalog.All)
            {
                var stk = DamageCalculator.ShotsToKill(w, BodyPart.Torso, 10f);
                switch (w.Category)
                {
                    case WeaponCategory.Pistol:
                        Assert.That(stk, Is.InRange(4, 5), w.WeaponId);
                        break;
                    case WeaponCategory.Smg:
                    case WeaponCategory.AssaultRifle:
                    case WeaponCategory.Lmg:
                        Assert.That(stk, Is.InRange(3, 5), w.WeaponId);
                        break;
                    case WeaponCategory.Dmr:
                        Assert.That(stk, Is.InRange(2, 3), w.WeaponId);
                        break;
                    case WeaponCategory.Sniper:
                        Assert.That(stk, Is.InRange(1, 2), w.WeaponId);
                        break;
                    case WeaponCategory.Shotgun:
                        Assert.That(stk, Is.InRange(1, 2), w.WeaponId);
                        break;
                }
            }
        }

        [Test]
        public void Ttk_SniperAndDmrHeadshotsOneTapUnarmored()
        {
            Assert.AreEqual(1, DamageCalculator.ShotsToKill(W(WeaponIds.Jng90), BodyPart.Head, 500f));
            Assert.AreEqual(1, DamageCalculator.ShotsToKill(W(WeaponIds.Knt76), BodyPart.Head, 100f));
            Assert.AreEqual(1, DamageCalculator.ShotsToKill(W(WeaponIds.Sar762Mt), BodyPart.Head, 100f));
            // Sv.3 kaskla JNG-90 kafası hâlâ tek atış.
            Assert.AreEqual(1, DamageCalculator.ShotsToKill(W(WeaponIds.Jng90), BodyPart.Head, 300f, 3, 0.55f));
        }

        [Test]
        public void Ttk_ArmorAddsShotsAndHigherLevelAddsMore()
        {
            var w = W(WeaponIds.Mpt76);
            var none = DamageCalculator.ShotsToKill(w, BodyPart.Torso, 20f);
            var v1 = DamageCalculator.ShotsToKill(w, BodyPart.Torso, 20f, 1, 0.30f);
            var v3 = DamageCalculator.ShotsToKill(w, BodyPart.Torso, 20f, 3, 0.55f);
            Assert.GreaterOrEqual(v1, none);
            Assert.Greater(v3, v1);
            Assert.AreEqual(4, v1);
            Assert.AreEqual(5, v3, "7.62 Sv.3 yeleği 2 atış ekler, hâlâ öldürücüdür");
            // 9 mm Sv.3'e karşı çok daha zayıf.
            Assert.GreaterOrEqual(DamageCalculator.ShotsToKill(W(WeaponIds.Sar109), BodyPart.Torso, 10f, 3, 0.55f), 8);
        }

        [Test]
        public void Ttk_ShotgunLethalOnlyAtShortRange()
        {
            var escort = W(WeaponIds.Escort);
            Assert.AreEqual(1, DamageCalculator.ShotsToKill(escort, BodyPart.Torso, 4f));
            Assert.Greater(DamageCalculator.ShotsToKill(escort, BodyPart.Torso, 30f), 1);
            var mag = W(WeaponIds.EscortMagnum);
            Assert.AreEqual(2, DamageCalculator.ShotsToKill(mag, BodyPart.Torso, 4f), "yarı otomatik pompalı tek atışta öldürmez");
        }

        [Test]
        public void Falloff_DamageNeverIncreasesWithDistance()
        {
            foreach (var w in WeaponCatalog.All)
            {
                var prev = float.MaxValue;
                for (var d = 0f; d <= w.FalloffEnd + 50f; d += 5f)
                {
                    var f = DamageCalculator.DistanceFactor(w, d);
                    Assert.LessOrEqual(f, prev + 1e-5f, w.WeaponId);
                    Assert.GreaterOrEqual(f, w.MinDamageFactor - 1e-5f, w.WeaponId);
                    prev = f;
                }
            }
        }

        [Test]
        public void BodyParts_HeadBeatsTorsoBeatsLimbs()
        {
            foreach (var w in WeaponCatalog.All)
            {
                Assert.Greater(DamageCalculator.BodyPartMultiplier(w, BodyPart.Head), 1f, w.WeaponId);
                Assert.Less(DamageCalculator.BodyPartMultiplier(w, BodyPart.Leg), 1f, w.WeaponId);
                Assert.Less(DamageCalculator.BodyPartMultiplier(w, BodyPart.Arm), 1f, w.WeaponId);
            }
        }

        // ---------------------------------------------------------------- Balistik

        [Test]
        public void Catalog_MuzzleVelocitiesAreRealWorldish()
        {
            foreach (var w in WeaponCatalog.All)
            {
                switch (w.AmmoType)
                {
                    case AmmoType.Mm9: Assert.That(w.MuzzleVelocity, Is.InRange(340f, 420f), w.WeaponId); break;
                    case AmmoType.Mm556: Assert.That(w.MuzzleVelocity, Is.InRange(850f, 950f), w.WeaponId); break;
                    case AmmoType.Mm762: Assert.That(w.MuzzleVelocity, Is.InRange(720f, 930f), w.WeaponId); break;
                    case AmmoType.Gauge12: Assert.That(w.MuzzleVelocity, Is.InRange(350f, 450f), w.WeaponId); break;
                }
            }

            Assert.Greater(W(WeaponIds.Jng90).MuzzleVelocity, W(WeaponIds.Mpt76).MuzzleVelocity, "uzun namlulu keskin nişancı daha hızlı");
            Assert.Less(W(WeaponIds.Mpt76K).MuzzleVelocity, W(WeaponIds.Mpt76).MuzzleVelocity, "kısa namlu daha yavaş");
        }

        [Test]
        public void Ballistics_DropAndTimeOfFlight()
        {
            var v = W(WeaponIds.Mpt76).MuzzleVelocity;
            var t100 = BallisticsMath.TimeOfFlight(v, AmmoType.Mm762, 100f);
            var t300 = BallisticsMath.TimeOfFlight(v, AmmoType.Mm762, 300f);
            Assert.Greater(t300, t100);
            Assert.That(t300, Is.InRange(0.33f, 0.45f));
            Assert.Greater(BallisticsMath.TimeOfFlight(v, AmmoType.Mm762, 300f), 300f / v - 1e-4f, "sürükleme süreyi uzatır");

            var d100 = BallisticsMath.Drop(v, AmmoType.Mm762, 100f);
            var d400 = BallisticsMath.Drop(v, AmmoType.Mm762, 400f);
            Assert.Greater(d400, d100 * 8f, "düşüş menzille hızlı büyür");
            Assert.That(d400, Is.InRange(0.9f, 1.8f));

            // 9 mm aynı menzilde 7.62'den çok daha çok düşer.
            Assert.Greater(BallisticsMath.Drop(W(WeaponIds.Sar9).MuzzleVelocity, AmmoType.Mm9, 100f),
                BallisticsMath.Drop(v, AmmoType.Mm762, 100f) * 5f);
            Assert.AreEqual(0f, BallisticsMath.Drop(v, AmmoType.Mm762, 0f), 1e-6f);
            Assert.Less(BallisticsMath.SpeedAt(v, AmmoType.Mm762, 500f), v * 0.75f);
            Assert.Greater(BallisticsMath.SpeedAt(v, AmmoType.Mm762, 500f), v * 0.65f);
            Assert.Less(BallisticsMath.DragPerMeter(AmmoType.Mm762), BallisticsMath.DragPerMeter(AmmoType.Mm556));
        }

        // ---------------------------------------------------------------- Elleme

        [Test]
        public void Handling_CatalogAdsTimeFollowsWeight()
        {
            foreach (var w in WeaponCatalog.All)
            {
                var expected = WeaponHandling.AdsTimeForWeight(w.Weight, w.HasScope);
                Assert.AreEqual(expected, w.AdsTime, 0.03f, w.WeaponId);
            }

            Assert.Less(W(WeaponIds.Sar9).AdsTime, W(WeaponIds.Mpt76).AdsTime);
            Assert.Less(W(WeaponIds.Mpt76).AdsTime, W(WeaponIds.Pmt76).AdsTime);
            Assert.Less(W(WeaponIds.Mpt76K).AdsTime, W(WeaponIds.G3).AdsTime);
        }

        [Test]
        public void Handling_SprintToFireGrowsWithWeight()
        {
            Assert.Less(WeaponHandling.SprintToFireSeconds(1f), WeaponHandling.SprintToFireSeconds(4f));
            Assert.Less(WeaponHandling.SprintToFireSeconds(4f), WeaponHandling.SprintToFireSeconds(11f));
            Assert.AreEqual(WeaponHandling.SprintToFireBase, WeaponHandling.SprintToFireSeconds(0f), 1e-5f);
            Assert.AreEqual(WeaponHandling.SprintToFireBase, WeaponHandling.SprintToFireSeconds(float.NaN), 1e-5f);
        }

        [Test]
        public void Runtime_BeginSprintRecoveryBlocksFireByWeight()
        {
            var pistol = new WeaponRuntimeService(W(WeaponIds.Sar9), new NullBus());
            var lmg = new WeaponRuntimeService(W(WeaponIds.Pmt76), new NullBus());
            pistol.BeginSprintRecovery();
            lmg.BeginSprintRecovery();
            Assert.IsFalse(pistol.TryTrigger(true, true));
            Assert.IsFalse(lmg.TryTrigger(true, true));

            pistol.Tick(pistol.SprintToFireSeconds + 0.01f);
            lmg.Tick(pistol.SprintToFireSeconds + 0.01f);
            Assert.IsTrue(pistol.TryTrigger(true, true), "tabanca çabuk hazır");
            Assert.IsFalse(lmg.TryTrigger(true, false), "makineli tüfek hâlâ hazır değil");
            lmg.Tick(lmg.SprintToFireSeconds);
            Assert.IsTrue(lmg.TryTrigger(true, true));
        }

        [Test]
        public void Runtime_BeginSprintRecoveryDoesNotShortenBoltCycle()
        {
            var jng = new WeaponRuntimeService(W(WeaponIds.Jng90), new NullBus());
            Assert.IsTrue(jng.TryTrigger(true, true));
            var before = jng.TimeUntilReady;
            jng.BeginSprintRecovery();
            Assert.AreEqual(before, jng.TimeUntilReady, 1e-5f);
        }

        // ---------------------------------------------------------------- Eklenti ödünleşimleri

        [Test]
        public void Attachments_TradeOffsAreReal()
        {
            var w = new WeaponRuntimeService(W(WeaponIds.Mpt55), new NullBus());
            var ads0 = w.AdsTime;
            var vel0 = w.MuzzleVelocity;
            var reload0 = w.ReloadDuration;
            Assert.AreEqual(W(WeaponIds.Mpt55).AdsTime, ads0, 1e-5f);
            Assert.AreEqual(W(WeaponIds.Mpt55).MuzzleVelocity, vel0, 1e-5f);

            Assert.IsTrue(w.TryAttach(ItemIds.Suppressor, out _));
            Assert.Less(w.MuzzleVelocity, vel0, "susturucu hızı düşürür");
            Assert.Greater(w.EffectiveWeight, W(WeaponIds.Mpt55).Weight);
            Assert.Greater(w.AdsTime, ads0, "ağırlık nişanı yavaşlatır");

            Assert.IsTrue(w.TryAttach(ItemIds.Scope4x, out _));
            var adsScoped = w.AdsTime;
            Assert.IsTrue(w.TryAttach(ItemIds.RedDot, out _));
            Assert.Greater(adsScoped, WeaponHandling.AdsTimeForWeight(0f, false), "4x belirgin ek süre");

            Assert.IsTrue(w.TryAttach(ItemIds.ExtMag, out _));
            Assert.IsTrue(w.TryAttach(ItemIds.VerticalGrip, out _));
            w.SetLoadedAmmo(0);
            w.TryBeginReload();
            Assert.Greater(w.ReloadDuration, reload0, "uzatılmış şarjör doldurmayı yavaşlatır");
            Assert.Greater(w.SprintToFireSeconds, WeaponHandling.SprintToFireSeconds(W(WeaponIds.Mpt55).Weight));
        }

        [Test]
        public void Attachments_NoAttachmentsLeavesRuntimeDefaults()
        {
            var m = AttachmentModifiers.None;
            Assert.AreEqual(0f, m.WeightKg);
            Assert.AreEqual(1f, m.AdsTimeMultiplier);
            Assert.AreEqual(1f, m.ReloadMultiplier);
            Assert.AreEqual(1f, m.VelocityMultiplier);
            Assert.AreEqual(0f, AttachmentStats.Compute(null).WeightKg);
            var c = AttachmentStats.Compute(new[] { ItemIds.Suppressor, ItemIds.VerticalGrip });
            Assert.AreEqual(0.4f + 0.25f, c.WeightKg, 1e-5f);
        }

        // ---------------------------------------------------------------- Sekme deseni

        [Test]
        public void Recoil_FirstTenShotsAreLearnableAndPerWeaponDistinct()
        {
            Assert.GreaterOrEqual(RecoilPattern.PatternLength, 10);
            foreach (var w in WeaponCatalog.All)
            {
                var seed = RecoilPattern.SeedFor(w.WeaponId);
                for (var i = 0; i < 10; i++)
                {
                    RecoilPattern.GetStep(seed, i, out var v1, out var h1);
                    RecoilPattern.GetStep(seed, i, out var v2, out var h2);
                    Assert.AreEqual(v1, v2);
                    Assert.AreEqual(h1, h2);
                    Assert.That(v1, Is.InRange(0.7f, 1.5f));
                    Assert.LessOrEqual(Math.Abs(h1), 1f);
                }
            }

            // Her silahın ilk 10 atış yatay izi farklı.
            for (var a = 0; a < WeaponCatalog.All.Count; a++)
            {
                for (var b = a + 1; b < WeaponCatalog.All.Count; b++)
                {
                    var sa = RecoilPattern.SeedFor(WeaponCatalog.All[a].WeaponId);
                    var sb = RecoilPattern.SeedFor(WeaponCatalog.All[b].WeaponId);
                    var diff = 0f;
                    for (var i = 1; i < 10; i++)
                    {
                        RecoilPattern.GetStep(sa, i, out _, out var ha);
                        RecoilPattern.GetStep(sb, i, out _, out var hb);
                        diff += Math.Abs(ha - hb);
                    }

                    Assert.Greater(diff, 0.05f, WeaponCatalog.All[a].WeaponId + " vs " + WeaponCatalog.All[b].WeaponId);
                }
            }
        }

        [Test]
        public void Recoil_PatternHasWeaponSpecificSideBias()
        {
            var left = 0;
            var right = 0;
            foreach (var w in WeaponCatalog.All)
            {
                var seed = RecoilPattern.SeedFor(w.WeaponId);
                var sum = 0f;
                for (var i = 4; i < 14; i++)
                {
                    RecoilPattern.GetStep(seed, i, out _, out var h);
                    sum += h;
                }

                if (sum < 0f)
                    left++;
                else
                    right++;
            }

            Assert.Greater(left, 0, "bazı silahlar sola kayar");
            Assert.Greater(right, 0, "bazı silahlar sağa kayar");
        }

        [Test]
        public void Recoil_RandomComponentIsBounded()
        {
            foreach (var r in new[] { 0f, 0.25f, 0.5f, 0.99f, 1f, -3f, 7f, float.NaN })
            {
                RecoilPattern.GetRandomComponent(r, out var vs, out var ha);
                Assert.LessOrEqual(Math.Abs(vs - 1f), RecoilPattern.RandomVerticalFraction + 1e-5f);
                Assert.LessOrEqual(Math.Abs(ha), RecoilPattern.RandomHorizontalFraction + 1e-5f);
            }
        }

        [Test]
        public void Recoil_RuntimeKickIsPatternPlusSmallRandom()
        {
            var w = new WeaponRuntimeService(W(WeaponIds.Mpt76), new NullBus());
            Assert.IsTrue(w.TryTrigger(true, true));
            w.GetRecoilKick(false, Stance.Standing, 0.5f, out var p1, out var y1);
            w.GetRecoilKick(false, Stance.Standing, 0.5f, out var p2, out var y2);
            Assert.AreEqual(p1, p2, "aynı rastgele değerle aynı sonuç");
            Assert.AreEqual(y1, y2);

            w.GetRecoilKick(false, Stance.Standing, 0.95f, out var p3, out var y3);
            var maxYawSwing = (2f * RecoilPattern.RandomHorizontalFraction) * w.Definition.RecoilHorizontal;
            Assert.LessOrEqual(Math.Abs(y3 - y1), maxYawSwing + 1e-4f);
            Assert.LessOrEqual(Math.Abs(p3 - p1), 2f * RecoilPattern.RandomVerticalFraction * w.Definition.RecoilVertical * 1.5f);
        }

        [Test]
        public void Recoil_RecoveryScalesWithWeaponAndAccumulation()
        {
            Assert.Greater(RecoilPattern.RecoveryDegPerSecond(1f, 5f), RecoilPattern.RecoveryDegPerSecond(1f, 0f));
            Assert.Greater(RecoilPattern.RecoveryDegPerSecond(1.3f, 3f), RecoilPattern.RecoveryDegPerSecond(0.6f, 3f));
            Assert.AreEqual(RecoilPattern.BaseRecoveryDegPerSecond, RecoilPattern.RecoveryDegPerSecond(1f, 0f), 1e-5f);
            Assert.AreEqual(RecoilPattern.BaseRecoveryDegPerSecond, RecoilPattern.RecoveryDegPerSecond(float.NaN, float.NaN), 1e-5f);
            Assert.Greater(W(WeaponIds.Sar9).RecoilRecovery, W(WeaponIds.Mg3).RecoilRecovery, "hafif silah daha çabuk toparlanır");
        }
    }
}
