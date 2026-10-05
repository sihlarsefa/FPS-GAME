using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode.Sim
{
    public sealed class BotDecisionTests
    {
        /// <summary>Sağlıklı, silahlı, düşman görmeyen, bölge içindeki bağımsız bot.</summary>
        private static BotSenses Calm() => new()
        {
            HealthNormalized = 1f,
            HasWeapon = true,
            HasAmmo = true,
            EnemyDistance = 999f,
            SecondsSinceEnemySeen = 999f,
            SecondsSinceDamaged = 999f,
            SecondsUntilZoneShrinks = 60f,
            NearestUsefulLootDistance = 999f
        };

        private static BotSenses Squad(SquadOrder? order, float distanceToLeader)
        {
            var s = Calm();
            s.IsSquadMember = true;
            s.LeaderAlive = true;
            s.DistanceToLeader = distanceToLeader;
            s.HasSquadOrder = order.HasValue;
            s.Order = order ?? SquadOrder.Follow;
            return s;
        }

        [Test]
        public void Calm_Independent_Roams()
        {
            var s = Calm();
            Assert.AreEqual(BotState.Roam, BotDecisionService.Decide(in s, BotState.Idle));
        }

        [Test]
        public void VisibleEnemy_Armed_Engages()
        {
            var s = Calm();
            s.CanSeeEnemy = true;
            s.EnemyDistance = 60f;
            s.SecondsSinceEnemySeen = 0f;
            Assert.AreEqual(BotState.Engage, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void VisibleEnemy_WhileReloading_StillEngages()
        {
            var s = Calm();
            s.HasAmmo = false;
            s.IsReloading = true;
            s.CanSeeEnemy = true;
            s.EnemyDistance = 30f;
            Assert.AreEqual(BotState.Engage, BotDecisionService.Decide(in s, BotState.Engage));
        }

        [Test]
        public void VisibleEnemy_Unarmed_Close_Punches()
        {
            var s = Calm();
            s.HasWeapon = false;
            s.CanSeeEnemy = true;
            s.EnemyDistance = 3f;
            Assert.AreEqual(BotState.Engage, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void VisibleEnemy_Unarmed_Far_FleesOrLoots()
        {
            var s = Calm();
            s.HasWeapon = false;
            s.CanSeeEnemy = true;
            s.EnemyDistance = 40f;
            Assert.AreEqual(BotState.Flee, BotDecisionService.Decide(in s, BotState.Roam));
            s.KnowsUsefulLoot = true;
            s.NeedsLoot = true;
            Assert.AreEqual(BotState.Loot, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void WeaponWithoutAmmo_IsTreatedAsUnarmed()
        {
            var s = Calm();
            s.HasAmmo = false;
            s.CanSeeEnemy = true;
            s.EnemyDistance = 50f;
            Assert.AreEqual(BotState.Flee, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void OutsideZone_MovesToZone()
        {
            var s = Calm();
            s.IsOutsideZone = true;
            s.HeardGunfireRecently = true;
            s.HealthNormalized = 0.3f;
            s.HasHealItem = true;
            Assert.AreEqual(BotState.MoveToZone, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void OutsideZone_ButEnemyVisible_Engages()
        {
            var s = Calm();
            s.IsOutsideZone = true;
            s.CanSeeEnemy = true;
            s.EnemyDistance = 20f;
            Assert.AreEqual(BotState.Engage, BotDecisionService.Decide(in s, BotState.MoveToZone));
        }

        [Test]
        public void LowHealth_WithHealItem_AndSafe_Heals()
        {
            var s = Calm();
            s.HealthNormalized = 0.5f;
            s.HasHealItem = true;
            s.SecondsSinceEnemySeen = 5f;
            Assert.AreEqual(BotState.Heal, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void LowHealth_EnemyRecentlySeen_DoesNotHeal()
        {
            var s = Calm();
            s.HealthNormalized = 0.5f;
            s.HasHealItem = true;
            s.SecondsSinceEnemySeen = 2f;
            Assert.AreNotEqual(BotState.Heal, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void LowHealth_NoHealItem_DoesNotHeal()
        {
            var s = Calm();
            s.HealthNormalized = 0.2f;
            Assert.AreNotEqual(BotState.Heal, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void Heal_ContinuesWithHysteresis()
        {
            var s = Calm();
            s.HealthNormalized = 0.7f;
            s.HasHealItem = true;
            s.SecondsSinceEnemySeen = 10f;
            Assert.AreEqual(BotState.Heal, BotDecisionService.Decide(in s, BotState.Heal));
            Assert.AreEqual(BotState.Roam, BotDecisionService.Decide(in s, BotState.Roam));
            s.HealthNormalized = 0.95f;
            Assert.AreEqual(BotState.Roam, BotDecisionService.Decide(in s, BotState.Heal));
        }

        [Test]
        public void HeardGunfire_Armed_Investigates()
        {
            var s = Calm();
            s.HeardGunfireRecently = true;
            Assert.AreEqual(BotState.Investigate, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void RecentlySeenEnemy_Investigates()
        {
            var s = Calm();
            s.HasLastKnownEnemyPosition = true;
            s.SecondsSinceEnemySeen = 3f;
            Assert.AreEqual(BotState.Investigate, BotDecisionService.Decide(in s, BotState.Engage));
            s.SecondsSinceEnemySeen = 30f;
            Assert.AreEqual(BotState.Roam, BotDecisionService.Decide(in s, BotState.Investigate));
        }

        [Test]
        public void HeardGunfire_Unarmed_DoesNotInvestigate()
        {
            var s = Calm();
            s.HasWeapon = false;
            s.HeardGunfireRecently = true;
            s.NeedsLoot = true;
            s.KnowsUsefulLoot = true;
            Assert.AreEqual(BotState.Loot, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void NeedsLoot_KnowsLoot_Loots()
        {
            var s = Calm();
            s.NeedsLoot = true;
            s.KnowsUsefulLoot = true;
            s.NearestUsefulLootDistance = 15f;
            Assert.AreEqual(BotState.Loot, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void OutsideNextZone_MovesToZone()
        {
            var s = Calm();
            s.IsOutsideNextZone = true;
            Assert.AreEqual(BotState.MoveToZone, BotDecisionService.Decide(in s, BotState.Roam));
        }

        [Test]
        public void Flee_PersistsBriefly()
        {
            var s = Calm();
            s.HasWeapon = false;
            s.SecondsSinceEnemySeen = 1f;
            Assert.AreEqual(BotState.Flee, BotDecisionService.Decide(in s, BotState.Flee));
            s.SecondsSinceEnemySeen = 5f;
            Assert.AreNotEqual(BotState.Flee, BotDecisionService.Decide(in s, BotState.Flee));
        }

        // ---- Tim emirleri ----

        [Test]
        public void SquadHold_Holds_EvenWithGunfire()
        {
            var s = Squad(SquadOrder.HoldPosition, 30f);
            s.HeardGunfireRecently = true;
            s.IsOutsideNextZone = true;
            Assert.AreEqual(BotState.Hold, BotDecisionService.Decide(in s, BotState.Follow));
        }

        [Test]
        public void SquadHold_EnemyVisible_Engages()
        {
            var s = Squad(SquadOrder.HoldPosition, 3f);
            s.CanSeeEnemy = true;
            s.EnemyDistance = 80f;
            Assert.AreEqual(BotState.Engage, BotDecisionService.Decide(in s, BotState.Hold));
        }

        [Test]
        public void SquadAttack_Assaults()
        {
            var s = Squad(SquadOrder.Attack, 20f);
            s.DistanceToOrderTarget = 150f;
            Assert.AreEqual(BotState.Assault, BotDecisionService.Decide(in s, BotState.Follow));
        }

        [Test]
        public void SquadAttack_EnemyVisible_Engages()
        {
            var s = Squad(SquadOrder.Attack, 20f);
            s.CanSeeEnemy = true;
            s.EnemyDistance = 40f;
            Assert.AreEqual(BotState.Engage, BotDecisionService.Decide(in s, BotState.Assault));
        }

        [TestCase(SquadOrder.HoldPosition)]
        [TestCase(SquadOrder.Attack)]
        [TestCase(SquadOrder.Follow)]
        [TestCase(SquadOrder.Regroup)]
        public void OutsideZone_BeatsSquadOrders(SquadOrder order)
        {
            var s = Squad(order, 30f);
            s.IsOutsideZone = true;
            Assert.AreEqual(BotState.MoveToZone, BotDecisionService.Decide(in s, BotState.Hold));
        }

        [TestCase(SquadOrder.Follow)]
        [TestCase(SquadOrder.Regroup)]
        public void SquadFollow_FarFromLeader_Follows(SquadOrder order)
        {
            var s = Squad(order, 12f);
            Assert.AreEqual(BotState.Follow, BotDecisionService.Decide(in s, BotState.Idle));
        }

        [TestCase(SquadOrder.Follow)]
        [TestCase(SquadOrder.Regroup)]
        public void SquadFollow_NearLeader_Idles(SquadOrder order)
        {
            var s = Squad(order, 5f);
            Assert.AreEqual(BotState.Idle, BotDecisionService.Decide(in s, BotState.Idle));
        }

        [Test]
        public void SquadFollow_Hysteresis()
        {
            var s = Squad(SquadOrder.Follow, 6f);
            Assert.AreEqual(BotState.Follow, BotDecisionService.Decide(in s, BotState.Follow), "yaklaşırken takibe devam");
            s.DistanceToLeader = 3f;
            Assert.AreEqual(BotState.Idle, BotDecisionService.Decide(in s, BotState.Follow));
        }

        [Test]
        public void SquadFollow_NearbyLoot_Loots_ButRegroupDoesNot()
        {
            var s = Squad(SquadOrder.Follow, 5f);
            s.NeedsLoot = true;
            s.KnowsUsefulLoot = true;
            s.NearestUsefulLootDistance = 6f;
            Assert.AreEqual(BotState.Loot, BotDecisionService.Decide(in s, BotState.Idle));

            s.Order = SquadOrder.Regroup;
            Assert.AreEqual(BotState.Idle, BotDecisionService.Decide(in s, BotState.Idle));
        }

        [Test]
        public void SquadFollow_LeaderDead_FallsBackToIndependent()
        {
            var s = Squad(SquadOrder.Follow, 50f);
            s.LeaderAlive = false;
            Assert.AreEqual(BotState.Roam, BotDecisionService.Decide(in s, BotState.Follow));
            s.HeardGunfireRecently = true;
            Assert.AreEqual(BotState.Investigate, BotDecisionService.Decide(in s, BotState.Follow));
        }

        [Test]
        public void SquadFollow_LowHealthSafe_HealsFirst()
        {
            var s = Squad(SquadOrder.Follow, 20f);
            s.HealthNormalized = 0.4f;
            s.HasHealItem = true;
            s.SecondsSinceEnemySeen = 10f;
            Assert.AreEqual(BotState.Heal, BotDecisionService.Decide(in s, BotState.Follow));
        }

        [Test]
        public void SquadNoOrder_StaysLeashedToLeader()
        {
            var s = Squad(null, 40f);
            s.HeardGunfireRecently = true;
            Assert.AreEqual(BotState.Follow, BotDecisionService.Decide(in s, BotState.Investigate));

            s.DistanceToLeader = 15f;
            Assert.AreEqual(BotState.Investigate, BotDecisionService.Decide(in s, BotState.Idle));

            s.HeardGunfireRecently = false;
            Assert.AreEqual(BotState.Follow, BotDecisionService.Decide(in s, BotState.Idle));

            s.DistanceToLeader = 5f;
            Assert.AreEqual(BotState.Idle, BotDecisionService.Decide(in s, BotState.Idle));

            s.AllyNeedsHelp = true;
            Assert.AreEqual(BotState.Investigate, BotDecisionService.Decide(in s, BotState.Idle));
        }

        [Test]
        public void SquadNoOrder_NearbyLoot_Loots()
        {
            var s = Squad(null, 8f);
            s.NeedsLoot = true;
            s.KnowsUsefulLoot = true;
            s.NearestUsefulLootDistance = 12f;
            Assert.AreEqual(BotState.Loot, BotDecisionService.Decide(in s, BotState.Idle));
            s.NearestUsefulLootDistance = 80f;
            Assert.AreEqual(BotState.Idle, BotDecisionService.Decide(in s, BotState.Idle));
        }

        [Test]
        public void StateNames_AreTurkish()
        {
            Assert.AreEqual("Çatışmada", BotDecisionService.GetStateName(BotState.Engage));
            Assert.AreEqual("Mevzide", BotDecisionService.GetStateName(BotState.Hold));
            Assert.AreEqual("Taarruzda", BotDecisionService.GetStateName(BotState.Assault));
        }
    }

    public sealed class BotDifficultyProfileTests
    {
        [TestCase(BotDifficulty.Easy, 6f, 0.9f, 160f)]
        [TestCase(BotDifficulty.Normal, 3.5f, 0.55f, 220f)]
        [TestCase(BotDifficulty.Hard, 1.8f, 0.3f, 280f)]
        public void For_ReturnsDocumentedValues(BotDifficulty difficulty, float aimError, float reaction, float view)
        {
            var p = BotDifficultyProfile.For(difficulty);
            Assert.AreEqual(aimError, p.AimErrorDegrees, 0.0001f);
            Assert.AreEqual(reaction, p.ReactionSeconds, 0.0001f);
            Assert.AreEqual(view, p.ViewDistance, 0.0001f);
            Assert.AreEqual(difficulty, p.Difficulty);
            Assert.LessOrEqual(p.BurstMin, p.BurstMax);
            Assert.Greater(p.BurstMin, 0);
            Assert.Greater(p.FieldOfViewDegrees, 0f);
            Assert.Greater(p.TurnSpeedDegreesPerSecond, 0f);
            Assert.Greater(p.HearingDistance, 0f);
            Assert.Greater(p.BurstPauseSeconds, 0f);
            Assert.Greater(p.DecisionIntervalSeconds, 0f);
            Assert.GreaterOrEqual(p.HeadshotChance, 0f);
            Assert.LessOrEqual(p.HeadshotChance, 1f);
            Assert.GreaterOrEqual(p.AggressionChance, 0f);
            Assert.LessOrEqual(p.AggressionChance, 1f);
        }

        [Test]
        public void Harder_IsStrictlyBetter()
        {
            var easy = BotDifficultyProfile.For(BotDifficulty.Easy);
            var normal = BotDifficultyProfile.For(BotDifficulty.Normal);
            var hard = BotDifficultyProfile.For(BotDifficulty.Hard);
            Assert.Greater(easy.AimErrorDegrees, normal.AimErrorDegrees);
            Assert.Greater(normal.AimErrorDegrees, hard.AimErrorDegrees);
            Assert.Greater(easy.ReactionSeconds, normal.ReactionSeconds);
            Assert.Greater(normal.ReactionSeconds, hard.ReactionSeconds);
            Assert.Less(easy.ViewDistance, normal.ViewDistance);
            Assert.Less(normal.ViewDistance, hard.ViewDistance);
            Assert.Less(easy.HeadshotChance, hard.HeadshotChance);
        }

        [Test]
        public void For_ReturnsNewInstance_UnknownFallsBackToNormal()
        {
            var a = BotDifficultyProfile.For(BotDifficulty.Normal);
            var b = BotDifficultyProfile.For(BotDifficulty.Normal);
            a.AimErrorDegrees = 99f;
            Assert.AreEqual(3.5f, b.AimErrorDegrees, 0.0001f);
            Assert.AreEqual(3.5f, BotDifficultyProfile.For((BotDifficulty)42).AimErrorDegrees, 0.0001f);
            var clone = b.Clone();
            Assert.AreEqual(b.ViewDistance, clone.ViewDistance, 0.0001f);
        }
    }
}
