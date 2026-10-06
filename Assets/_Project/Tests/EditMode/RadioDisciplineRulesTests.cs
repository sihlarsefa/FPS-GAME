#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RadioDisciplineRulesTests
    {
        [Test]
        public void Topic_OrderIsContactWoundBombMovementStatus()
        {
            Assert.Greater((int)RadioDisciplineRules.TopicOf("contact"), (int)RadioDisciplineRules.TopicOf("wound_5"));
            Assert.Greater((int)RadioDisciplineRules.TopicOf("wound_5"), (int)RadioDisciplineRules.TopicOf("arty_hit"));
            Assert.Greater((int)RadioDisciplineRules.TopicOf("arty_hit"), (int)RadioDisciplineRules.TopicOf("order"));
            Assert.Greater((int)RadioDisciplineRules.TopicOf("order"), (int)RadioDisciplineRules.TopicOf("reload"));
        }

        [Test]
        public void Priority_ContactBeatsStatusAndCommanderBumps()
        {
            Assert.AreEqual(RadioPriority.High, RadioDisciplineRules.PriorityFor(RadioTopic.Contact, RadioPriority.Normal, false));
            Assert.AreEqual(RadioPriority.Low, RadioDisciplineRules.PriorityFor(RadioTopic.Status, RadioPriority.Low, false));
            Assert.AreEqual(RadioPriority.Normal, RadioDisciplineRules.PriorityFor(RadioTopic.Status, RadioPriority.Low, true));
            Assert.AreEqual(RadioPriority.Critical, RadioDisciplineRules.PriorityFor(RadioTopic.Contact, RadioPriority.Critical, true));
        }

        [Test]
        public void Cooldown_AtLeastEightSecondsExceptOrders()
        {
            Assert.AreEqual(8f, RadioDisciplineRules.EffectiveCooldown("reload", 4f));
            Assert.AreEqual(12f, RadioDisciplineRules.EffectiveCooldown("reload", 12f));
            Assert.AreEqual(1.5f, RadioDisciplineRules.EffectiveCooldown("order", 1.5f));
        }

        [Test]
        public void Callsign_OnlyBeyondTwoHundredMeters()
        {
            Assert.IsFalse(RadioDisciplineRules.NeedsCallsign(200f));
            Assert.IsTrue(RadioDisciplineRules.NeedsCallsign(200.5f));
            Assert.AreEqual("Kuzgun-2, Kuzgun-1... Temas!", RadioDisciplineRules.WithCallsign("Temas!", 1, 2));
        }

        [Test]
        public void Queue_CommanderFirstThenOrderedAndCapped()
        {
            var q = new RadioDisciplineQueue<string>();
            Assert.IsTrue(q.Enqueue(new RadioQueued(RadioPriority.Normal, RadioTopic.Bomb, false, 0f), "bomb"));
            Assert.IsTrue(q.Enqueue(new RadioQueued(RadioPriority.Normal, RadioTopic.Wounded, true, 0.5f), "cmd"));
            Assert.IsTrue(q.Enqueue(new RadioQueued(RadioPriority.High, RadioTopic.Contact, false, 1f), "contact"));
            Assert.IsFalse(q.Enqueue(new RadioQueued(RadioPriority.Normal, RadioTopic.Status, false, 1.1f), "weak"));
            Assert.AreEqual(3, q.Count);
            Assert.IsTrue(q.TryDequeue(1.2f, out var a)); Assert.AreEqual("contact", a);
            Assert.IsTrue(q.TryDequeue(1.2f, out a)); Assert.AreEqual("cmd", a);
            Assert.IsTrue(q.TryDequeue(1.2f, out a)); Assert.AreEqual("bomb", a);
            Assert.IsFalse(q.TryDequeue(1.2f, out _));
        }

        [Test]
        public void Queue_ExpiresOldAndBusyBeepRateLimited()
        {
            var q = new RadioDisciplineQueue<string>();
            q.Enqueue(new RadioQueued(RadioPriority.High, RadioTopic.Contact, false, 0f), "old");
            Assert.IsFalse(q.TryDequeue(6f, out _));
            Assert.IsFalse(RadioDisciplineRules.ShouldBusyBeep(1f, 0f));
            Assert.IsTrue(RadioDisciplineRules.ShouldBusyBeep(3f, 0f));
        }
    }
}
#endif
