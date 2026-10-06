using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    public sealed class HitFeedbackRulesTests
    {
        [Test]
        public void Pitch_RisesWithDamage_AndClamps()
        {
            Assert.Less(HitTones.PitchForDamage(10f), HitTones.PitchForDamage(60f));
            Assert.AreEqual(HitTones.MaxPitch, HitTones.PitchForDamage(500f), 1e-4f);
            Assert.AreEqual(HitTones.MinPitch, HitTones.PitchForDamage(-5f), 1e-4f);
        }

        [Test]
        public void Select_ArmorVsFleshVsKill()
        {
            Assert.AreEqual(SoundId.HitArmor, HitTones.Select(30f, false, false, true).Layer);
            Assert.AreEqual(SoundId.HitHelmet, HitTones.Select(30f, true, false, true).Layer);
            Assert.AreEqual(SoundId.HitFlesh, HitTones.Select(30f, false, false, false).Layer);
            Assert.AreEqual(SoundId.Headshot, HitTones.Select(30f, true, false, false).Marker);
            var kill = HitTones.Select(30f, false, true, false);
            Assert.AreEqual(SoundId.KillConfirm, kill.Marker);
            Assert.IsFalse(kill.HasLayer);
        }

        [Test]
        public void ShouldPlay_ThrottlesButNotKills()
        {
            Assert.IsFalse(HitTones.ShouldPlay(false, 1.01f, 1f));
            Assert.IsTrue(HitTones.ShouldPlay(false, 1.1f, 1f));
            Assert.IsTrue(HitTones.ShouldPlay(true, 1.001f, 1f));
        }

        [Test]
        public void Stacker_CombinesWithinWindow_SeparatesAfter()
        {
            var s = new DamageNumberStacker();
            var v = new PlayerId(3);
            var a = s.Add(v, 20f, false, false, false, 1f);
            var b = s.Add(v, 25f, true, false, false, 1.1f);
            Assert.AreEqual(a, b);
            Assert.AreEqual(45f, s.Slots[a].Total, 1e-4f);
            Assert.IsTrue(s.Slots[a].Headshot);
            var c = s.Add(v, 10f, false, false, false, 2f);
            Assert.AreNotEqual(a, c);
            var d = s.Add(new PlayerId(4), 10f, false, false, false, 2f);
            Assert.AreNotEqual(c, d);
        }

        [Test]
        public void Stacker_ArmorOnlyIfAllArmored()
        {
            var s = new DamageNumberStacker();
            var i = s.Add(new PlayerId(1), 10f, false, false, true, 0f);
            s.Add(new PlayerId(1), 10f, false, false, false, 0.1f);
            Assert.IsFalse(s.Slots[i].Armor);
        }

        [Test]
        public void Assist_RequiresDamageNotKillerAndWindow()
        {
            var s = new DamageNumberStacker();
            var v = new PlayerId(7);
            s.Add(v, 30f, false, false, false, 1f);
            Assert.IsFalse(s.IsAssist(v, true, 2f));
            Assert.IsTrue(s.IsAssist(v, false, 2f));
            Assert.IsFalse(s.IsAssist(v, false, 2f), "tek seferlik");
            s.Add(v, 10f, false, false, false, 3f);
            Assert.IsFalse(s.IsAssist(v, false, 4f), "eşik altı");
            s.Add(new PlayerId(8), 50f, false, false, false, 5f);
            Assert.IsFalse(s.IsAssist(new PlayerId(8), false, 20f), "pencere dışı");
        }

        [Test]
        public void LongRange_DelayAndThresholds()
        {
            Assert.AreEqual(0f, HitTones.ConfirmDelay(149f, 800f));
            Assert.AreEqual(300f / 850f, HitTones.ConfirmDelay(300f, 0f), 1e-4f);
            Assert.AreEqual(HitTones.MaxConfirmDelay, HitTones.ConfirmDelay(5000f, 800f), 1e-4f);
            Assert.IsFalse(HitTones.IsFarKill(249f, true));
            Assert.IsTrue(HitTones.IsFarKill(250f, true));
            Assert.IsFalse(HitTones.IsFarKill(400f, false));
        }

        [Test]
        public void LongRange_ArmorRingThinsAndHeadshotHigher()
        {
            Assert.AreEqual(1f, HitTones.ArmorRingScale(100f), 1e-4f);
            Assert.AreEqual(0.35f, HitTones.ArmorRingScale(900f), 1e-4f);
            Assert.Less(HitTones.SelectFar(400f, false, false, true).LayerVolume, HitTones.SelectFar(160f, false, false, true).LayerVolume);
            Assert.Greater(HitTones.SelectFar(300f, true, false, false).MarkerPitch, HitTones.SelectFar(300f, false, false, false).MarkerPitch);
        }
    }
}
