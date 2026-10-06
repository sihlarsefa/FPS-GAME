using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class TutorialV2Tests
    {
        private static TutorialService One(TutorialCondition c)
        {
            var step = new TutorialStepDef { Id = "s", RewardXp = 5 };
            step.Conditions.Add(c);
            var svc = new TutorialService(new[] { step });
            svc.Start();
            return svc;
        }

        [Test]
        public void IsKillFalse_IgnoresKillingHit()
        {
            var svc = One(new TutorialCondition { Key = "HitConfirmedEvent", IsKill = 0 });
            svc.Signal("HitConfirmedEvent", null, TutorialSignalData.Kill(true));
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("HitConfirmedEvent", null, TutorialSignalData.Kill(false));
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void MinRadius_RejectsSmallExplosions()
        {
            var svc = One(new TutorialCondition { Key = "ExplosionEvent", MinRadius = 1f });
            svc.Signal("ExplosionEvent", null, TutorialSignalData.Blast(0.5f));
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("ExplosionEvent", null, TutorialSignalData.Blast(3f));
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void IsImpactFalse_OnlyCallPhase()
        {
            var svc = One(new TutorialCondition { Key = "ArtilleryStrikeEvent", IsImpact = 0 });
            svc.Signal("ArtilleryStrikeEvent", null, TutorialSignalData.Strike(true));
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("ArtilleryStrikeEvent", null, TutorialSignalData.Strike(false));
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void MinSeconds_AndWhileAiming()
        {
            var svc = One(new TutorialCondition { Key = "AdsActive", MinSeconds = 1f, WhileAiming = true });
            var aiming = false;
            svc.AimingProbe = () => aiming;
            svc.Signal("AdsActive", null, TutorialSignalData.Held(2f));
            Assert.IsFalse(svc.IsCompleted);
            aiming = true;
            svc.Signal("AdsActive", null, TutorialSignalData.Held(0.5f));
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("AdsActive", null, TutorialSignalData.Held(1.2f));
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void WaypointQualifier_MustMatch()
        {
            var svc = One(new TutorialCondition { Key = "PlayerReachedWaypoint", Qualifier = "pad" });
            svc.Signal("PlayerReachedWaypoint", "other");
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("PlayerReachedWaypoint", "pad");
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void AddExperience_RaisesXp()
        {
            var career = new CareerStatsService(null);
            career.Load();
            var gained = career.AddExperience(250);
            Assert.AreEqual(250, gained);
            Assert.AreEqual(250, career.Current.Experience);
            Assert.AreEqual(0, career.AddExperience(-5));
        }
    }
}
