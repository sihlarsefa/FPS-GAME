using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class DeathRecapTests
    {
        [Test]
        public void Build_AggregatesHitsByPartAndWindow()
        {
            var b = new DeathRecapBuilder();
            b.Add(new DamageTaken(1f, 10f, BodyPart.Torso, 5, "w", 40f));   // pencere dışı (ölüm 20, pencere 8)
            b.Add(new DamageTaken(14f, 20f, BodyPart.Torso, 5, "w", 35f));
            b.Add(new DamageTaken(15f, 30f, BodyPart.Leg, 5, "w", 30f));
            b.Add(new DamageTaken(19.5f, 80f, BodyPart.Head, 5, "w", 28f));
            var r = b.Build(20f);
            Assert.AreEqual(3, r.TotalHits);
            Assert.AreEqual(130f, r.TotalDamage, 0.01f);
            Assert.AreEqual(1, r.HitsByPart[(int)BodyPart.Head]);
            Assert.IsTrue(r.FinalHeadshot);
            Assert.AreEqual(28f, r.Distance, 0.01f);
            Assert.IsTrue(r.HasKiller);
        }

        [Test]
        public void Build_IgnoresZeroDamage_AndCapsTimeline()
        {
            var b = new DeathRecapBuilder();
            b.Add(new DamageTaken(1f, 0f, BodyPart.Torso, 1, null, 0f));
            Assert.AreEqual(0, b.Count);
            for (var i = 0; i < 20; i++)
                b.Add(new DamageTaken(10f + i * 0.1f, 5f, BodyPart.Arm, -1, null, 0f));
            var r = b.Build(12f);
            Assert.AreEqual(DeathRecapBuilder.MaxTimeline, r.Timeline.Count);
            Assert.IsFalse(r.HasKiller);
        }

        [Test]
        public void Builder_StoreIsBounded()
        {
            var b = new DeathRecapBuilder();
            for (var i = 0; i < 200; i++) b.Add(new DamageTaken(i, 1f, BodyPart.Torso, 1, null, 0f));
            Assert.AreEqual(DeathRecapBuilder.MaxStored, b.Count);
        }

        [Test]
        public void Text_Turkish()
        {
            Assert.AreEqual("Kafa", DeathRecapText.PartName(BodyPart.Head));
            Assert.AreEqual("Bacak", DeathRecapText.PartName(BodyPart.Leg));
            Assert.AreEqual("43 m", DeathRecapText.Distance(42.6f));
            Assert.AreEqual("-0,8 sn", DeathRecapText.RelativeTime(9.2f, 10f));
            Assert.AreEqual("-12", DeathRecapText.Damage(12.2f));
            Assert.AreEqual("TİM SIRASI: #3 / 10", DeathRecapText.Placement(3, 10));
            Assert.AreEqual("02:05", DeathRecapText.Survival(125f));
            Assert.AreEqual("KAFADAN VURULDUN", DeathRecapText.Headline(true, true));
            Assert.AreEqual("ŞEHİT DÜŞTÜN", DeathRecapText.Headline(false, true));
            Assert.AreEqual("45 / 100 CAN", DeathRecapText.KillerHealth(44.2f, 100f));
        }

        [Test]
        public void DownedHud_Math()
        {
            Assert.AreEqual(0.5f, DownedHudMath.BleedFraction(22.5f, 45f), 0.001f);
            Assert.AreEqual(0f, DownedHudMath.BleedFraction(-3f, 45f));
            Assert.IsTrue(DownedHudMath.ReviveInterrupted(0.6f, 0f, true));
            Assert.IsFalse(DownedHudMath.ReviveInterrupted(0.6f, 0.62f, true));
            Assert.IsFalse(DownedHudMath.ReviveInterrupted(0.6f, 0f, false));
            Assert.IsTrue(DownedHudMath.CanCall(10f, 4f));
            Assert.IsFalse(DownedHudMath.CanCall(6f, 4f));
            Assert.Greater(DownedHudMath.PulseHz(0f), DownedHudMath.PulseHz(1f));
            StringAssert.StartsWith("YARDIM İSTENDİ", DownedHudMath.CallButtonText(5f, 4f, "[SOL TIK]"));
            StringAssert.StartsWith("[SOL TIK]", DownedHudMath.CallButtonText(50f, 4f, "[SOL TIK]"));
        }
    }
}
