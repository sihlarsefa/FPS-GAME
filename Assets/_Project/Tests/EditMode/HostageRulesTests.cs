using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    public class HostageRulesTests
    {
        [Test]
        public void Interact_TogglesFollowAndHold()
        {
            var r = new HostageRules(2, 480f);
            Assert.AreEqual(HostageState.Following, r.Interact(0));
            Assert.AreEqual(HostageState.Holding, r.Interact(0));
            Assert.AreEqual(HostageState.Following, r.Interact(0));
            Assert.AreEqual(HostageState.Captive, r.GetState(1));
            Assert.IsTrue(r.AnyFreed);
        }

        [Test]
        public void HostageDeath_Fails()
        {
            var r = new HostageRules(2, 480f);
            r.MarkDead(1);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(HostageOutcome.HostageKilled, r.Outcome);
        }

        [Test]
        public void AllExtracted_Succeeds()
        {
            var r = new HostageRules(2, 480f);
            r.Interact(0);
            r.Interact(1);
            r.MarkExtracted(0);
            Assert.IsFalse(r.IsOver);
            r.MarkExtracted(1);
            Assert.IsTrue(r.IsSuccess);
        }

        [Test]
        public void CaptiveCannotBeExtracted()
        {
            var r = new HostageRules(1, 480f);
            r.MarkExtracted(0);
            Assert.AreEqual(HostageState.Captive, r.GetState(0));
            Assert.IsFalse(r.IsOver);
        }

        [Test]
        public void Timeout_Fails_AndResultIsFinal()
        {
            var r = new HostageRules(2, 10f);
            r.Tick(11f);
            Assert.AreEqual(HostageOutcome.TimeUp, r.Outcome);
            r.Interact(0);
            r.SquadLost();
            Assert.AreEqual(HostageOutcome.TimeUp, r.Outcome);
            Assert.AreEqual(0f, r.TimeRemaining);
        }

        [Test]
        public void DefaultDuration_IsEightMinutes()
        {
            Assert.AreEqual(480f, new HostageRules().DurationSeconds);
        }

        [Test]
        public void ExtractionRules_NeedLandedVehicleAndDistance()
        {
            Assert.IsTrue(HostageRules.CanExtract(5f, true));
            Assert.IsFalse(HostageRules.CanExtract(5f, false));
            Assert.IsFalse(HostageRules.CanExtract(40f, true));
            Assert.IsTrue(HostageRules.InInteractRange(2f));
            Assert.IsFalse(HostageRules.InInteractRange(6f));
        }

        [Test]
        public void SquadLost_Fails()
        {
            var r = new HostageRules();
            r.SquadLost();
            Assert.AreEqual(HostageOutcome.SquadLost, r.Outcome);
        }
    }
}
