#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>P1-bot-taktik: insansı nişan, baskı, siper skoru/peek, geri çekilme, işitme, sis altında kaldırma kuralları.</summary>
    public class BotCombatRulesTests
    {
        // ------------------------------------------------------------------ beceri / tepki

        [Test]
        public void Reaction_AlwaysWithin180To450ms()
        {
            foreach (BotDifficulty d in System.Enum.GetValues(typeof(BotDifficulty)))
            {
                for (var rank = 0; rank <= (int)MilitaryRank.Albay; rank++)
                {
                    for (var r = 0; r <= 10; r++)
                    {
                        var skill = BotSkill.Skill01(d, (MilitaryRank)rank, r / 10f);
                        var t = BotSkill.ReactionSeconds(skill, r / 10f);
                        Assert.GreaterOrEqual(t, 0.18f);
                        Assert.LessOrEqual(t, 0.45f);
                    }
                }
            }
        }

        [Test]
        public void Reaction_HardFasterThanEasy_RankHelps()
        {
            var easy = BotSkill.ReactionSeconds(BotSkill.Skill01(BotDifficulty.Easy, MilitaryRank.Er, 0.5f), 0.5f);
            var hard = BotSkill.ReactionSeconds(BotSkill.Skill01(BotDifficulty.Hard, MilitaryRank.Er, 0.5f), 0.5f);
            Assert.Less(hard, easy);

            var lowRank = BotSkill.Skill01(BotDifficulty.Normal, MilitaryRank.Er, 0.5f);
            var highRank = BotSkill.Skill01(BotDifficulty.Normal, MilitaryRank.Yuzbasi, 0.5f);
            Assert.Greater(highRank, lowRank);
        }

        [Test]
        public void Tiers_FollowSkill()
        {
            Assert.AreEqual(BotSkillTier.Acemi, BotSkill.TierOf(BotSkill.Skill01(BotDifficulty.Easy, MilitaryRank.Er, 0.5f)));
            Assert.AreEqual(BotSkillTier.Muvazzaf, BotSkill.TierOf(BotSkill.Skill01(BotDifficulty.Normal, MilitaryRank.Er, 0.5f)));
            Assert.AreEqual(BotSkillTier.Seckin, BotSkill.TierOf(BotSkill.Skill01(BotDifficulty.Hard, MilitaryRank.Albay, 1f)));
        }

        [Test]
        public void AimSettle_ConvergesOverTime_AndSkillShrinksInitialError()
        {
            var start = BotSkill.AimSettle(0f, 0.3f);
            var later = BotSkill.AimSettle(1.5f, 0.3f);
            var settled = BotSkill.AimSettle(10f, 0.3f);
            Assert.Greater(start, later);
            Assert.Greater(later, settled);
            Assert.AreEqual(BotSkill.SettledAimFactor, settled, 0.02f);
            Assert.Less(BotSkill.AimSettle(0f, 0.95f), BotSkill.AimSettle(0f, 0.1f));
        }

        [Test]
        public void Recoil_ImperfectAndWorsensWithBurstAndSuppression()
        {
            var steady = BotSkill.RecoilControl(0.5f, 0f, 0.5f, 0);
            Assert.Greater(BotSkill.RecoilControl(0.5f, 0f, 0.5f, 15), steady);
            Assert.Greater(BotSkill.RecoilControl(0.5f, 0.9f, 0.5f, 0), steady);
            Assert.Less(BotSkill.RecoilControl(0.9f, 0f, 0.5f, 0), BotSkill.RecoilControl(0.1f, 0f, 0.5f, 0));
            Assert.AreNotEqual(BotSkill.RecoilControl(0.5f, 0f, 0.1f, 0), BotSkill.RecoilControl(0.5f, 0f, 0.9f, 0));
        }

        [Test]
        public void TargetSwitchDelay_GrowsWithAngle_ShorterForDeadTarget()
        {
            var near = BotSkill.TargetSwitchDelay(0.5f, 10f, false, 0.5f);
            var far = BotSkill.TargetSwitchDelay(0.5f, 150f, false, 0.5f);
            Assert.Greater(far, near);
            Assert.Less(BotSkill.TargetSwitchDelay(0.5f, 90f, true, 0.5f), BotSkill.TargetSwitchDelay(0.5f, 90f, false, 0.5f));
            Assert.Less(BotSkill.TargetSwitchDelay(0.9f, 90f, false, 0.5f), BotSkill.TargetSwitchDelay(0.1f, 90f, false, 0.5f));
        }

        // ------------------------------------------------------------------ baskı

        [Test]
        public void Suppression_AccumulatesAndDecays()
        {
            var s = new BotSuppression();
            s.Add(BotSuppression.DamageImpulse);
            s.Add(BotSuppression.DamageImpulse);
            Assert.AreEqual(0.44f, s.Level, 0.001f);
            s.Tick(1f);
            Assert.Less(s.Level, 0.44f);
            s.Tick(10f);
            Assert.AreEqual(0f, s.Level, 0.0001f);
            for (var i = 0; i < 20; i++)
                s.Add(0.3f);
            Assert.AreEqual(1f, s.Level, 0.0001f);
        }

        [Test]
        public void Suppression_NearMissAndNearbyFire()
        {
            Assert.Greater(BotSuppression.NearMissAmount(0.3f, 1f), BotSuppression.NearMissAmount(2.5f, 1f));
            Assert.AreEqual(0f, BotSuppression.NearMissAmount(3.5f, 1f));
            Assert.Greater(BotSuppression.NearbyFireAmount(5f), BotSuppression.NearbyFireAmount(30f));
            Assert.AreEqual(0f, BotSuppression.NearbyFireAmount(40f));
        }

        [Test]
        public void Suppression_ReducesAccuracyAndBurst_VeteransResist()
        {
            Assert.Greater(BotSkill.SuppressedAimMultiplier(0.8f), BotSkill.SuppressedAimMultiplier(0.1f));
            Assert.Less(BotSkill.SuppressedBurstScale(0.9f), BotSkill.SuppressedBurstScale(0f));
            Assert.Less(BotSkill.EffectiveSuppression(0.8f, 0.95f), BotSkill.EffectiveSuppression(0.8f, 0.1f));
        }

        // ------------------------------------------------------------------ siper skoru / peek

        [Test]
        public void CoverScore_PrefersNearCover_PenalizesSecondaryExposure()
        {
            var near = BotCombatRules.CoverScore(5f, 30f, 35f, 30f, false, true, 5f, false, 0f);
            var far = BotCombatRules.CoverScore(18f, 30f, 35f, 30f, false, true, 5f, false, 0f);
            Assert.Less(near, far);

            var hiddenBoth = BotCombatRules.CoverScore(8f, 30f, 35f, 30f, true, true, 5f, false, 0f);
            var exposed = BotCombatRules.CoverScore(8f, 30f, 35f, 30f, true, false, 5f, false, 0f);
            Assert.Greater(exposed, hiddenBoth);
        }

        [Test]
        public void CoverScore_PenalizesApproachingThreat_AloneAndRewardsHighCover()
        {
            var holds = BotCombatRules.CoverScore(8f, 35f, 35f, 30f, false, true, 5f, false, 0f);
            var closing = BotCombatRules.CoverScore(8f, 20f, 35f, 30f, false, true, 5f, false, 0f);
            Assert.Greater(closing, holds);

            var together = BotCombatRules.CoverScore(8f, 35f, 35f, 30f, false, true, 5f, false, 0f);
            var alone = BotCombatRules.CoverScore(8f, 35f, 35f, 30f, false, true, 60f, false, 0f);
            Assert.Greater(alone, together);

            Assert.Less(BotCombatRules.CoverScore(8f, 35f, 35f, 30f, false, true, 5f, true, 0f), holds);
        }

        [Test]
        public void CoverScore_UnderFireTravelCostsMore()
        {
            var calm = BotCombatRules.CoverScore(12f, 35f, 35f, 30f, false, true, 5f, false, 0f);
            var pinned = BotCombatRules.CoverScore(12f, 35f, 35f, 30f, false, true, 5f, false, 1f);
            Assert.Greater(pinned, calm);
        }

        [Test]
        public void Peek_WillingnessDropsUnderFireAndLowHealth()
        {
            var calm = BotCombatRules.PeekWillingness(0f, 0.5f, 1f);
            Assert.AreEqual(1f, calm, 0.001f);
            Assert.Less(BotCombatRules.PeekWillingness(0.9f, 0.5f, 1f), calm);
            Assert.Less(BotCombatRules.PeekWillingness(0f, 0.5f, 0.3f), calm);
            Assert.Greater(BotCombatRules.PeekWillingness(0.9f, 0.95f, 1f), BotCombatRules.PeekWillingness(0.9f, 0.05f, 1f));
            Assert.GreaterOrEqual(BotCombatRules.PeekWillingness(1f, 0f, 0f), 0.08f);
        }

        [Test]
        public void Peek_DurationsReactToSuppression()
        {
            Assert.Greater(BotCombatRules.HiddenSeconds(0.9f, 0.5f, 0.5f), BotCombatRules.HiddenSeconds(0f, 0.5f, 0.5f));
            Assert.Less(BotCombatRules.PeekSeconds(0.9f, 0.5f, 0.5f), BotCombatRules.PeekSeconds(0f, 0.5f, 0.5f));
            Assert.GreaterOrEqual(BotCombatRules.PeekSeconds(1f, 0f, 0f), 0.6f);
            Assert.IsTrue(BotCombatRules.PhaseElapsed(5f, 5f));
            Assert.IsFalse(BotCombatRules.PhaseElapsed(4.9f, 5f));
        }

        // ------------------------------------------------------------------ geri çekilme / kanat / bomba

        [Test]
        public void FallBack_WhenOutnumberedOrCriticallyHurt()
        {
            Assert.IsFalse(BotCombatRules.ShouldFallBack(1f, 0, 0, 0f, 1f, 0.5f), "düşman yok");
            Assert.IsTrue(BotCombatRules.ShouldFallBack(0.7f, 3, 1, 0f, 1f, 0.5f), "3 vs 2");
            Assert.IsFalse(BotCombatRules.ShouldFallBack(1f, 2, 3, 0f, 1f, 0.5f), "dostlar çok");
            Assert.IsTrue(BotCombatRules.ShouldFallBack(0.2f, 1, 0, 0f, 1f, 0.5f), "tek başına yaralı");
            Assert.IsFalse(BotCombatRules.ShouldFallBack(0.9f, 1, 2, 0f, 1f, 0.5f), "1 vs 3 sağlıklı");
        }

        [Test]
        public void PinnedFlank_NeedsTimeHealthAndFewEnemies()
        {
            Assert.IsFalse(BotCombatRules.ShouldFlankWhenPinned(1f, 0.6f, 1f, 40f, 1, 0f, 0.5f), "yeterince sıkışmadı");
            Assert.IsFalse(BotCombatRules.ShouldFlankWhenPinned(6f, 0.6f, 0.3f, 40f, 1, 0f, 0.5f), "yaralı");
            Assert.IsFalse(BotCombatRules.ShouldFlankWhenPinned(6f, 0.6f, 1f, 40f, 4, 0f, 0.5f), "çok düşman");
            Assert.IsFalse(BotCombatRules.ShouldFlankWhenPinned(6f, 0.95f, 1f, 40f, 1, 0f, 0.5f), "ezilmiş");
            Assert.IsTrue(BotCombatRules.ShouldFlankWhenPinned(6f, 0.6f, 1f, 40f, 1, 0f, 0.5f));
            var a = BotCombatRules.PinnedFlankAngle(0f);
            Assert.GreaterOrEqual(a, 45f);
            Assert.LessOrEqual(BotCombatRules.PinnedFlankAngle(1f), 75f);
        }

        [Test]
        public void Crawl_OnlyShortHeavilySuppressed()
        {
            Assert.IsTrue(BotCombatRules.ShouldCrawlToCover(0.8f, 3f));
            Assert.IsFalse(BotCombatRules.ShouldCrawlToCover(0.3f, 3f));
            Assert.IsFalse(BotCombatRules.ShouldCrawlToCover(0.8f, 12f));
        }

        [Test]
        public void FlushGrenade_IncreasesWithHiddenAndPinnedTime()
        {
            var baseline = BotCombatRules.FlushGrenadeChance(false, 0.5f, 0f, 0f);
            Assert.Greater(BotCombatRules.FlushGrenadeChance(false, 0.5f, 6f, 0f), baseline);
            Assert.Greater(BotCombatRules.FlushGrenadeChance(false, 0.5f, 0f, 6f), baseline);
            Assert.Greater(BotCombatRules.FlushGrenadeChance(true, 0.5f, 0f, 0f), baseline);
            Assert.LessOrEqual(BotCombatRules.FlushGrenadeChance(true, 1f, 20f, 20f), 1f);
        }

        // ------------------------------------------------------------------ sis altında kaldırma

        [Test]
        public void ReviveUnderSmoke_RequiresSmokeHealthAndLowSuppression()
        {
            Assert.IsTrue(BotCombatRules.CanReviveUnderSmoke(true, 0.8f, 0.2f, 10f, 1));
            Assert.IsFalse(BotCombatRules.CanReviveUnderSmoke(false, 0.8f, 0.2f, 10f, 1), "sis yok");
            Assert.IsFalse(BotCombatRules.CanReviveUnderSmoke(true, 0.3f, 0.2f, 10f, 1), "can düşük");
            Assert.IsFalse(BotCombatRules.CanReviveUnderSmoke(true, 0.8f, 0.8f, 10f, 1), "ezilmiş");
            Assert.IsFalse(BotCombatRules.CanReviveUnderSmoke(true, 0.8f, 0.2f, 40f, 1), "uzak");
            Assert.IsFalse(BotCombatRules.CanReviveUnderSmoke(true, 0.8f, 0.2f, 10f, 3), "çok düşman");

            Assert.IsTrue(BotCombatRules.ShouldSmokeForRevive(true, false, true, 12f));
            Assert.IsFalse(BotCombatRules.ShouldSmokeForRevive(true, true, true, 12f), "sis zaten var");
            Assert.IsFalse(BotCombatRules.ShouldSmokeForRevive(true, false, false, 12f), "sis eli yok");
            Assert.IsFalse(BotCombatRules.ShouldSmokeForRevive(false, false, true, 12f), "tehdit yok");
        }

        // ------------------------------------------------------------------ işitme

        [Test]
        public void Hearing_OcclusionShrinksRange()
        {
            Assert.AreEqual(1f, BotCombatRules.OcclusionFactor(0));
            Assert.Less(BotCombatRules.OcclusionFactor(1), 1f);
            Assert.Less(BotCombatRules.OcclusionFactor(2), BotCombatRules.OcclusionFactor(1));
            Assert.GreaterOrEqual(BotCombatRules.OcclusionFactor(99), 0.25f);

            var open = BotCombatRules.GunfireAudibleRange(85f, 1f, 0);
            var walled = BotCombatRules.GunfireAudibleRange(85f, 1f, 2);
            Assert.AreEqual(85f, open, 0.001f);
            Assert.Less(walled, open * 0.5f);
            Assert.Greater(BotCombatRules.GunfireAudibleRange(85f, 2f, 0), open);
        }

        [Test]
        public void Hearing_FootstepRangeByMovement()
        {
            Assert.AreEqual(0f, BotCombatRules.FootstepRange(0.2f, false, false));
            var sprint = BotCombatRules.FootstepRange(6f, false, false);
            var run = BotCombatRules.FootstepRange(4f, false, false);
            var walk = BotCombatRules.FootstepRange(2f, false, false);
            var crouch = BotCombatRules.FootstepRange(2f, true, false);
            var prone = BotCombatRules.FootstepRange(2f, false, true);
            Assert.Greater(sprint, run);
            Assert.Greater(run, walk);
            Assert.Greater(walk, crouch);
            Assert.Greater(crouch, prone);
        }

        [Test]
        public void Hearing_PositionErrorGrowsWithDistanceAndWalls()
        {
            Assert.Greater(BotCombatRules.HeardPositionError(60f, 0), BotCombatRules.HeardPositionError(20f, 0));
            Assert.Greater(BotCombatRules.HeardPositionError(40f, 2), BotCombatRules.HeardPositionError(40f, 0));
        }

        // ------------------------------------------------------------------ sıçramalı ilerleme

        [Test]
        public void Bound_OneTeamMovesWhileOtherCovers_AndPointAdvancesTowardEnemy()
        {
            var now = 1.0f;
            Assert.AreNotEqual(BotSquadTactics.MovesThisPhase(0, now), BotSquadTactics.MovesThisPhase(1, now));
            var wait = BotSquadTactics.SecondsToNextPhase(now);
            Assert.Greater(wait, 0f);
            Assert.LessOrEqual(wait, BotSquadTactics.BoundPeriodSeconds);
            Assert.AreNotEqual(BotSquadTactics.MovesThisPhase(1, now), BotSquadTactics.MovesThisPhase(1, now + wait + 0.01f));

            var self = Vector3.zero;
            var enemy = new Vector3(0f, 0f, 50f);
            var p = BotSquadTactics.BoundPoint(self, enemy, 10f, 0f);
            Assert.AreEqual(10f, p.z, 0.01f);
            var lateral = BotSquadTactics.BoundPoint(self, enemy, 10f, 3f);
            Assert.AreEqual(3f, Mathf.Abs(lateral.x), 0.01f);
        }

        [Test]
        public void Bound_RequiresAlliesRangeHealthAndLowSuppression()
        {
            Assert.IsTrue(BotSquadTactics.ShouldBound(40f, 1f, 0.1f, 2));
            Assert.IsFalse(BotSquadTactics.ShouldBound(40f, 1f, 0.1f, 0), "dost yok");
            Assert.IsFalse(BotSquadTactics.ShouldBound(10f, 1f, 0.1f, 2), "çok yakın");
            Assert.IsFalse(BotSquadTactics.ShouldBound(40f, 0.4f, 0.1f, 2), "yaralı");
            Assert.IsFalse(BotSquadTactics.ShouldBound(40f, 1f, 0.7f, 2), "bastırılmış");
        }

        // ------------------------------------------------------------------ RC2 v3

        [Test]
        public void V3_SuppressiveBurstAndWindowBounds()
        {
            for (var i = 0; i <= 10; i++)
            {
                var r = i / 10f;
                Assert.That(BotCombatRules.SuppressiveBurstRounds(r), Is.InRange(3, 5));
                Assert.That(BotCombatRules.SuppressWindowSeconds(r), Is.InRange(4f, 8f));
            }
        }

        [Test]
        public void V3_SuppressCoverEdge_RequiresLmgOrArAndFlanker()
        {
            Assert.IsTrue(BotCombatRules.ShouldSuppressCoverEdge(WeaponCategory.AssaultRifle, 0.8f, true, true, 40f));
            Assert.IsFalse(BotCombatRules.ShouldSuppressCoverEdge(WeaponCategory.AssaultRifle, 0.8f, true, false, 40f));
            Assert.IsTrue(BotCombatRules.ShouldSuppressCoverEdge(WeaponCategory.Lmg, 0.8f, true, false, 40f));
            Assert.IsFalse(BotCombatRules.ShouldSuppressCoverEdge(WeaponCategory.Sniper, 0.8f, true, true, 40f));
            Assert.IsFalse(BotCombatRules.ShouldSuppressCoverEdge(WeaponCategory.Lmg, 0.1f, true, true, 40f));
            Assert.IsFalse(BotCombatRules.ShouldSuppressCoverEdge(WeaponCategory.Lmg, 0.8f, false, true, 40f));
        }

        [Test]
        public void V3_Frag_RangeCookAndFriendlySafety()
        {
            Assert.IsFalse(BotCombatRules.FragRangeOk(11.9f));
            Assert.IsTrue(BotCombatRules.FragRangeOk(12f));
            Assert.IsTrue(BotCombatRules.FragRangeOk(30f));
            Assert.IsFalse(BotCombatRules.FragRangeOk(30.1f));
            Assert.AreEqual(1.5f, BotCombatRules.FragCookSeconds(true, true, 4f), 1e-4f);
            Assert.AreEqual(0f, BotCombatRules.FragCookSeconds(false, true, 4f));
            Assert.AreEqual(0f, BotCombatRules.FragCookSeconds(true, false, 4f));
            Assert.IsFalse(BotCombatRules.FragSafeFromFriendlies(6f, 5f));
            Assert.IsTrue(BotCombatRules.FragSafeFromFriendlies(8f, 5f));
        }

        [Test]
        public void V3_DoorEntry_PhasesProgressPauseThenPeekThenEnter()
        {
            var p = BotCombatRules.DoorEntryPhase.Approach;
            p = BotCombatRules.NextDoorPhase(p);
            Assert.AreEqual(BotCombatRules.DoorEntryPhase.Pause, p);
            p = BotCombatRules.NextDoorPhase(p);
            Assert.AreEqual(BotCombatRules.DoorEntryPhase.Peek, p);
            p = BotCombatRules.NextDoorPhase(p);
            Assert.AreEqual(BotCombatRules.DoorEntryPhase.Enter, p);
            Assert.AreEqual(p, BotCombatRules.NextDoorPhase(p));
            Assert.Greater(BotCombatRules.DoorPhaseSeconds(BotCombatRules.DoorEntryPhase.Pause, 0.5f), 0.5f);
            Assert.IsTrue(BotCombatRules.ShouldPauseAtDoor(2f, false, false));
            Assert.IsFalse(BotCombatRules.ShouldPauseAtDoor(2f, true, false));
            Assert.IsFalse(BotCombatRules.ShouldPauseAtDoor(5f, false, false));
        }

        [Test]
        public void V3_Wounded_LimpAndBandage()
        {
            Assert.Less(BotCombatRules.LimpSpeedScale(true), 1f);
            Assert.AreEqual(1f, BotCombatRules.LimpSpeedScale(false));
            Assert.IsTrue(BotCombatRules.ShouldBandageNow(true, 0.9f, true, 6f, 0));
            Assert.IsFalse(BotCombatRules.ShouldBandageNow(true, 0.9f, true, 2f, 0), "tehdit yeni");
            Assert.IsFalse(BotCombatRules.ShouldBandageNow(true, 0.9f, true, 6f, 1), "düşman görünür");
            Assert.IsFalse(BotCombatRules.ShouldBandageNow(true, 0.9f, false, 6f, 0), "bandaj yok");
            Assert.IsFalse(BotCombatRules.ShouldBandageNow(false, 0.95f, true, 6f, 0), "sağlam");
        }

        [Test]
        public void V3_LastMan_TurtlesAndCommanderCallsArtillery()
        {
            Assert.Less(BotCombatRules.LastManAggression(0.8f, 0), 0.8f);
            Assert.AreEqual(0.8f, BotCombatRules.LastManAggression(0.8f, 2));
            Assert.IsTrue(BotCombatRules.LastManShouldCallArtillery(0, true, true, true));
            Assert.IsFalse(BotCombatRules.LastManShouldCallArtillery(0, false, true, true));
            Assert.IsFalse(BotCombatRules.LastManShouldCallArtillery(1, true, true, true));
        }
    }
}
#endif
