using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class LimbDamageTests
    {
        [Test]
        public void SingleLeg_Slows25Percent()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Leg, 20f, false, false, false);
            Assert.AreEqual(0.75f, s.MoveSpeedMultiplier, 1e-4f);
        }

        [Test]
        public void TwoLegs_CapAt40Percent_AndNoMoreStacking()
        {
            var s = new LimbDamageState();
            for (var i = 0; i < 6; i++)
                s.OnHit(BodyPart.Leg, 20f, false, false, false);
            Assert.AreEqual(2, s.LegWounds);
            Assert.AreEqual(0.6f, s.MoveSpeedMultiplier, 1e-4f);
        }

        [Test]
        public void ArmWound_RaisesSwayAndAdsTime_Stacking()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Arm, 15f, false, false, false);
            Assert.AreEqual(1.3f, s.SwayMultiplier, 1e-4f);
            Assert.AreEqual(1.3f, s.AdsTimeMultiplier, 1e-4f);
            s.OnHit(BodyPart.Arm, 15f, false, false, false);
            Assert.AreEqual(1.6f, s.SwayMultiplier, 1e-4f);
        }

        [Test]
        public void TinyHit_IsNoWound()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Leg, 1f, false, false, false);
            Assert.IsFalse(s.HasWounds);
        }

        [Test]
        public void UnprotectedTorso_Bleeds_OneHpPerThreeSeconds_For15Seconds()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Torso, 20f, true, false, false);
            Assert.IsTrue(s.IsBleeding);
            var total = 0f;
            for (var i = 0; i < 400; i++)
                total += s.Tick(0.1f);
            Assert.AreEqual(5f, total, 1e-3f);
            Assert.IsFalse(s.IsBleeding);
        }

        [Test]
        public void ProtectedTorso_DoesNotBleed()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Torso, 20f, false, false, false);
            Assert.IsFalse(s.IsBleeding);
        }

        [Test]
        public void RepeatedTorsoHit_RefreshesNotStacks()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Torso, 20f, true, false, false);
            s.Tick(6f);
            s.OnHit(BodyPart.Torso, 20f, true, false, false);
            Assert.AreEqual(LimbDamageRules.BleedDurationSeconds, s.BleedRemaining, 1e-4f);
        }

        [Test]
        public void Bandage_ClearsAllWounds()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Leg, 20f, false, false, false);
            s.OnHit(BodyPart.Arm, 20f, false, false, false);
            s.OnHit(BodyPart.Torso, 20f, true, false, false);
            s.Bandage();
            Assert.IsFalse(s.HasWounds);
            Assert.AreEqual(1f, s.MoveSpeedMultiplier, 1e-4f);
            Assert.AreEqual(0f, s.Tick(10f));
        }

        [Test]
        public void HelmetGraze_Flinches1_2s_AndFiresHook()
        {
            var s = new LimbDamageState();
            var fired = 0;
            s.GrazeFlinched += () => fired++;
            s.OnHit(BodyPart.Head, 12f, false, true, false);
            Assert.AreEqual(1, fired);
            Assert.IsTrue(s.IsFlinching);
            s.Tick(1.0f);
            Assert.IsTrue(s.IsFlinching);
            s.Tick(0.3f);
            Assert.IsFalse(s.IsFlinching);
        }

        [Test]
        public void HeadHit_WithoutHelmet_OrLethal_NoFlinch()
        {
            var s = new LimbDamageState();
            s.OnHit(BodyPart.Head, 12f, false, false, false);
            s.OnHit(BodyPart.Head, 12f, false, true, true);
            s.OnHit(BodyPart.Head, 90f, false, true, false);
            Assert.IsFalse(s.IsFlinching);
        }

        [Test]
        public void EnvironmentalSources_AreRecognised()
        {
            Assert.IsTrue(LimbDamageRules.IsEnvironmentalSource("bleed"));
            Assert.IsTrue(LimbDamageRules.IsEnvironmentalSource("bleedout"));
            Assert.IsFalse(LimbDamageRules.IsEnvironmentalSource("ak47"));
        }
    }
}
