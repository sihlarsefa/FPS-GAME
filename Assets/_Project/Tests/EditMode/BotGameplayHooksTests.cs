using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Characters;

namespace Project.Tests.EditMode
{
    public class BotGameplayHooksTests
    {
        [Test]
        public void ReactionRespectsDifficultyRanges()
        {
            for (var r = 0f; r <= 1f; r += 0.25f)
            for (var s = 0f; s <= 1f; s += 0.25f)
            {
                var easy = BotSkill.ReactionSeconds(BotDifficulty.Easy, s, r);
                Assert.That(easy, Is.InRange(0.35f, 0.9f));
                var hard = BotSkill.ReactionSeconds(BotDifficulty.Hard, s, r);
                Assert.That(hard, Is.InRange(0.18f, 0.45f));
                var normal = BotSkill.ReactionSeconds(BotDifficulty.Normal, s, r);
                Assert.That(normal, Is.InRange(0.25f, 0.6f));
            }
        }

        [Test]
        public void ReactionIsDeterministicAndEasySlowerThanHard()
        {
            Assert.AreEqual(BotSkill.ReactionSeconds(BotDifficulty.Normal, 0.5f, 0.3f), BotSkill.ReactionSeconds(BotDifficulty.Normal, 0.5f, 0.3f));
            Assert.Greater(BotSkill.ReactionSeconds(BotDifficulty.Easy, 0.2f, 0.5f), BotSkill.ReactionSeconds(BotDifficulty.Hard, 0.2f, 0.5f));
        }

        [Test]
        public void BreathingIntervalShortensWithIntensity()
        {
            Assert.IsTrue(float.IsPositiveInfinity(PlayerBreathing.Interval(0.1f)));
            Assert.Greater(PlayerBreathing.Interval(0.3f), PlayerBreathing.Interval(1f));
            Assert.Greater(PlayerBreathing.Volume(1f), PlayerBreathing.Volume(0f));
        }

        [Test]
        public void LeanStepConverges()
        {
            var v = 0f;
            for (var i = 0; i < 60; i++) v = SoldierLeanApplier.Step(v, 1f, 0.02f);
            Assert.That(v, Is.GreaterThan(0.95f));
        }
    }
}
