#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.AI;
using Project.Application.Dialogue;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class TeammateAiTests
    {
        [Test]
        public void Cover_NearHighBetweenThreat_BeatsFarLow()
        {
            var self = new Float3(0, 0, 0);
            var threat = new Float3(0, 0, 40);
            var good = new TeammateCoverScoring.Candidate(new Float3(0, 0, 5), 1.8f);
            var bad = new TeammateCoverScoring.Candidate(new Float3(0, 0, -20), 0.4f);
            Assert.Greater(TeammateCoverScoring.Score(self, threat, good), TeammateCoverScoring.Score(self, threat, bad));
        }

        [Test]
        public void Cover_PickBest_ReturnsMinusOneWhenNone()
        {
            var self = new Float3(0, 0, 0);
            var threat = new Float3(0, 0, 40);
            var arr = new[] { new TeammateCoverScoring.Candidate(new Float3(0, 0, 3), 0.1f) };
            Assert.AreEqual(-1, TeammateCoverScoring.PickBest(self, threat, arr, 1));
            Assert.AreEqual(-1, TeammateCoverScoring.PickBest(self, threat, null, 0));
        }

        [Test]
        public void Cover_PickBest_ChoosesHighCover()
        {
            var self = new Float3(0, 0, 0);
            var threat = new Float3(0, 0, 40);
            var arr = new[]
            {
                new TeammateCoverScoring.Candidate(new Float3(0, 0, 4), 0.5f),
                new TeammateCoverScoring.Candidate(new Float3(0, 0, 6), 1.8f)
            };
            Assert.AreEqual(1, TeammateCoverScoring.PickBest(self, threat, arr, 2));
        }

        [Test]
        public void Cover_HeightAndDistanceScores_AreMonotonic()
        {
            Assert.Greater(TeammateCoverScoring.HeightScore(1.8f), TeammateCoverScoring.HeightScore(1.0f));
            Assert.Greater(TeammateCoverScoring.DistanceScore(3f), TeammateCoverScoring.DistanceScore(15f));
            Assert.AreEqual(0f, TeammateCoverScoring.DistanceScore(100f));
        }

        [Test]
        public void Callout_ContactClock_FrontAndRight()
        {
            var bot = new Float3(0, 0, 0);
            Assert.AreEqual(12, TeammateCallouts.ContactClock(0f, bot, new Float3(0, 0, 30)));
            Assert.AreEqual(3, TeammateCallouts.ContactClock(0f, bot, new Float3(30, 0, 0)));
            Assert.AreEqual(6, TeammateCallouts.ContactClock(0f, bot, new Float3(0, 0, -30)));
        }

        [Test]
        public void Callout_MagStatus()
        {
            Assert.AreEqual(CalloutKind.MagEmpty, TeammateCallouts.MagStatus(0, 30, 2));
            Assert.AreEqual(CalloutKind.MagLow, TeammateCallouts.MagStatus(5, 30, 2));
            Assert.AreEqual(CalloutKind.None, TeammateCallouts.MagStatus(25, 30, 2));
            Assert.AreEqual(CalloutKind.None, TeammateCallouts.MagStatus(0, 0, 2));
        }

        [Test]
        public void Callout_Progress_AndCategory()
        {
            Assert.AreEqual(CalloutKind.Advancing, TeammateCallouts.Progress(false, true, false, 99f));
            Assert.AreEqual(CalloutKind.Holding, TeammateCallouts.Progress(true, false, false, 99f));
            Assert.AreEqual(CalloutKind.None, TeammateCallouts.Progress(false, true, true, 1f));
            Assert.AreEqual(DialogueCats.Advance, TeammateCallouts.Category(CalloutKind.Advancing));
            Assert.AreEqual(DialogueCats.MagEmpty, TeammateCallouts.Category(CalloutKind.MagEmpty));
        }

        [Test]
        public void Formation_LeaderZero_AndWingsSymmetric()
        {
            Assert.AreEqual(0f, TeammateFormation.LocalOffset(0).Magnitude);
            var l = TeammateFormation.LocalOffset(1);
            var r = TeammateFormation.LocalOffset(2);
            Assert.AreEqual(-r.X, l.X, 0.001f);
            Assert.AreEqual(r.Z, l.Z, 0.001f);
            Assert.IsTrue(l.Z < 0f);
        }

        [Test]
        public void Formation_WorldSlot_RotatesWithYaw()
        {
            var leader = new Float3(10, 0, 10);
            var p0 = TeammateFormation.WorldSlot(leader, 0f, 2);    // sağ-arka
            Assert.Greater(p0.X, 10f);
            Assert.Greater(10f, p0.Z);
            var p90 = TeammateFormation.WorldSlot(leader, 90f, 2);  // lider doğuya bakar: arka = -X, sağ = -Z
            Assert.Greater(10f, p90.X);
            Assert.Greater(10f, p90.Z);
        }

        [Test]
        public void Formation_Speed()
        {
            Assert.AreEqual(0f, TeammateFormation.SpeedFor(0.2f, 3f, 6f));
            Assert.AreEqual(3f, TeammateFormation.SpeedFor(1f, 3f, 6f));
            Assert.AreEqual(6f, TeammateFormation.SpeedFor(10f, 3f, 6f));
        }

        [Test]
        public void Aid_Decisions()
        {
            var self = new Float3(0, 0, 0);
            Assert.AreEqual(AidAction.MoveToWounded, TeammateAid.Decide(self, new Float3(10, 0, 0), true, true, false, false, 1f));
            Assert.AreEqual(AidAction.Revive, TeammateAid.Decide(self, new Float3(1, 0, 0), true, true, false, false, 1f));
            Assert.AreEqual(AidAction.CoverWounded, TeammateAid.Decide(self, new Float3(5, 0, 0), true, true, true, false, 1f));
            Assert.AreEqual(AidAction.None, TeammateAid.Decide(self, new Float3(5, 0, 0), true, false, false, false, 1f));
            Assert.AreEqual(AidAction.None, TeammateAid.Decide(self, new Float3(5, 0, 0), true, true, false, false, 0.1f));
        }

        [Test]
        public void Aid_ClosestHelper()
        {
            var helpers = new[] { new Float3(20, 0, 0), new Float3(5, 0, 0), new Float3(2, 0, 0) };
            var ok = new[] { true, true, false };
            Assert.AreEqual(1, TeammateAid.ClosestHelper(Float3.Zero, helpers, ok, 3));
            Assert.AreEqual(-1, TeammateAid.ClosestHelper(Float3.Zero, helpers, new[] { false, false, false }, 3));
        }
    }
}
#endif
