#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class SoldierDetailRulesTests
    {
        [Test]
        public void FaceCover_PerPalette()
        {
            Assert.AreEqual(FaceCoverKind.Balaclava, SoldierDetailRules.FaceCoverFor(1, 0.99f));
            Assert.AreEqual(FaceCoverKind.Shemagh, SoldierDetailRules.FaceCoverFor(2, 0.1f));
            Assert.AreEqual(FaceCoverKind.None, SoldierDetailRules.FaceCoverFor(0, 0.5f));
            Assert.AreEqual(FaceCoverKind.Balaclava, SoldierDetailRules.FaceCoverFor(0, 0.05f));
        }

        [Test]
        public void SoldierLook_ForTeam_AssignsAndClonesFaceCover()
        {
            var look = SoldierLook.ForTeam(1, null);
            Assert.AreEqual(FaceCoverKind.Balaclava, look.FaceCover);
            Assert.AreEqual(FaceCoverKind.Balaclava, look.Clone().FaceCover);
        }

        [Test]
        public void BackpackBackZ_GrowsWithLevel()
        {
            Assert.Less(SoldierDetailRules.BackpackBackZ(2), SoldierDetailRules.BackpackBackZ(1));
            Assert.Less(SoldierDetailRules.BackpackBackZ(3), SoldierDetailRules.BackpackBackZ(2));
        }

        [Test]
        public void HipSway_ZeroWhenProneOrIdle()
        {
            Assert.AreEqual(0f, SoldierDetailRules.HipSway(1f, 0f, 0f, 0f), 1e-6f);
            Assert.AreEqual(0f, SoldierDetailRules.HipSway(1f, 1f, 1f, 0f), 1e-6f);
            Assert.Greater(SoldierDetailRules.HipSway(1f, 1f, 0f, 0f), 0f);
        }

        [Test]
        public void AntennaTilt_LeansBackWithSpeed()
        {
            Assert.Less(SoldierDetailRules.AntennaTilt(5f, 0f), SoldierDetailRules.AntennaTilt(0f, 0f));
            Assert.Less(SoldierDetailRules.ChestCounterTwist(1f, 1f, true), SoldierDetailRules.ChestCounterTwist(1f, 1f, false));
        }

        [Test]
        public void Weary_SlumpAndBreathAmplitudes()
        {
            Assert.AreEqual(0f, SoldierDetailRules.WearySlumpDrop(0f), 1e-6f);
            Assert.That(SoldierDetailRules.WearySlumpDrop(1f), Is.InRange(0.02f, 0.04001f));
            Assert.AreEqual(1.2f, SoldierDetailRules.WearyBreathAmplitude(1f), 1e-5f);
            Assert.AreEqual(1f, SoldierDetailRules.WearyBreathAmplitude(0f), 1e-5f);
        }

        [Test]
        public void Weary_HeadIntervalSeededInRange()
        {
            for (var i = 0; i < 50; i++)
            {
                var v = SoldierDetailRules.WearyHeadInterval(1234, i);
                Assert.That(v, Is.InRange(9f, 14f));
                Assert.AreEqual(v, SoldierDetailRules.WearyHeadInterval(1234, i));
            }
            Assert.Greater(SoldierDetailRules.WearyHeadInterval(1, 0) + SoldierDetailRules.WearyHeadInterval(1, 1)
                           + SoldierDetailRules.WearyHeadInterval(1, 2), 27f);
        }

        [Test]
        public void Weary_HeadDroopRisesAndReturns()
        {
            Assert.AreEqual(0f, SoldierDetailRules.WearyHeadDroopPitch(0f, 1f), 1e-5f);
            Assert.Greater(SoldierDetailRules.WearyHeadDroopPitch(2f, 1f), 10f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyHeadDroopPitch(3.6f, 1f), 1e-5f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyHeadShakeYaw(1f, 3, 0), 1e-6f);
            Assert.That(Mathf.Abs(SoldierDetailRules.WearyHeadShakeYaw(3.6f, 3, 0)), Is.LessThan(5.01f));
        }

        [Test]
        public void Weary_BrowWipe_1p2Seconds()
        {
            Assert.AreEqual(0f, SoldierDetailRules.WearyBrowWipeWeight(0f), 1e-6f);
            Assert.AreEqual(1f, SoldierDetailRules.WearyBrowWipeWeight(0.6f), 1e-6f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyBrowWipeWeight(1.2f), 1e-6f);
            Assert.AreEqual(1.2f, SoldierDetailRules.WearyBrowWipeDuration, 1e-6f);
        }

        [Test]
        public void Weary_TremorOnlyAboveThreshold()
        {
            Assert.AreEqual(0f, SoldierDetailRules.WearyTremorAmplitude(0.7f), 1e-6f);
            Assert.Greater(SoldierDetailRules.WearyTremorAmplitude(0.9f), 0f);
        }

        [Test]
        public void Weary_Effective_ZeroWhenMovingAimingOrDead()
        {
            Assert.Greater(SoldierDetailRules.WearyEffective(1f, 0f, false, 0f, 0f, false, 0f, false), 0.99f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyEffective(1f, 3f, false, 0f, 0f, false, 0f, false), 1e-6f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyEffective(1f, 0f, true, 0f, 0f, false, 0f, false), 1e-6f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyEffective(1f, 0f, false, 0f, 0f, false, 0f, true), 1e-6f);
            Assert.AreEqual(0f, SoldierDetailRules.WearyEffective(1f, 0f, false, 1f, 0f, false, 0f, false), 1e-6f);
        }

        [Test]
        public void Weary_FreeHand_NoneWithRifle()
        {
            Assert.AreEqual(-1, SoldierDetailRules.WearyFreeHand(true, false, 7));
            Assert.AreEqual(0, SoldierDetailRules.WearyFreeHand(false, true, 7));
            Assert.That(SoldierDetailRules.WearyFreeHand(false, false, 7), Is.InRange(0, 1));
        }

        [Test]
        public void Weary_InhaleCrossedOncePerPeriod()
        {
            var n = 0;
            for (var t = 0f; t < 9.1f; t += 0.05f)
                if (SoldierDetailRules.WearyInhaleCrossed(t, t + 0.05f)) n++;
            Assert.That(n, Is.InRange(1, 3));
        }
    }
}
#endif
