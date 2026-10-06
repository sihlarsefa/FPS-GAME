#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Combat;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Combat;
using Project.Application.Combat.Suppression;
using UnityEngine;

namespace Project.Tests
{
    public class BulletFlybyTests
    {
        private static BulletPassInfo Info(float v, float miss, float along) =>
            new BulletPassInfo(Vector3.zero, new Vector3(0, 0, along), miss, along, v, AmmoType.Mm556);

        [Test]
        public void Supersonic_NearPass_HasCrackAndThumpGap()
        {
            var p = BulletFlybyAudio.PlanFor(Info(900f, 1f, 200f));
            Assert.IsTrue(p.Crack);
            Assert.Greater(p.ThumpDelay, 0f);
        }

        [Test]
        public void Subsonic_NoCrack_NoGap()
        {
            var p = BulletFlybyAudio.PlanFor(Info(250f, 1f, 100f));
            Assert.IsFalse(p.Crack);
            Assert.AreEqual(0f, p.ThumpDelay, 0.0001f);
        }

        [Test]
        public void CloserPass_IsLouder()
        {
            Assert.Greater(BulletFlybyAudio.PlanFor(Info(900f, 0.3f, 100f)).Volume, BulletFlybyAudio.PlanFor(Info(900f, 2.5f, 100f)).Volume);
        }

        [Test]
        public void MuffleCutoff_OpenToClosed()
        {
            Assert.AreEqual(MixerRouting.MuffleOpenHz, MixerRouting.MuffleCutoffFor(0f), 1f);
            Assert.AreEqual(MixerRouting.MuffleClosedHz, MixerRouting.MuffleCutoffFor(1f), 1f);
        }

        [Test]
        public void Impulse_AddsLevelAndMuffle()
        {
            var s = new SuppressionState();
            s.AddImpulse(0.3f, true);
            Assert.Greater(s.Level, 0f);
            Assert.IsTrue(s.IsMuffled);
            s.Tick(0.6f);
            Assert.IsFalse(s.IsMuffled);
        }
    }
}
#endif
