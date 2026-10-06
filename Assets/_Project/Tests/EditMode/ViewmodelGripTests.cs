using NUnit.Framework;
using Project.Infrastructure.Weapons;
using Project.Infrastructure.Weapons.Viewmodel;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class ViewmodelGripTests
    {
        [Test]
        public void GripClass_MapsStyles()
        {
            Assert.AreEqual(GripClass.Pistol, GripClasses.From(WeaponStyle.Tp9));
            Assert.AreEqual(GripClass.Sniper, GripClasses.From(WeaponStyle.Jng90));
            Assert.AreEqual(GripClass.Lmg, GripClasses.From(WeaponStyle.Mg3));
            Assert.AreEqual(GripClass.None, GripClasses.From(WeaponStyle.None));
        }

        [Test]
        public void Wrist_SmallBend_Unchanged()
        {
            var fore = Vector3.forward;
            var hand = Quaternion.Euler(10f, 8f, 0f);
            var r = WristLimit.Clamp(fore, hand, 55f, 32f);
            Assert.Less(Quaternion.Angle(hand, r), 0.01f);
        }

        [Test]
        public void Wrist_LargeBend_ClampedWithinLimits()
        {
            var fore = Vector3.forward;
            var hand = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.identity;
            var r = WristLimit.Clamp(fore, hand, 55f, 32f);
            var bend = WristLimit.BendAngle(fore, r);
            Assert.Less(bend, 56f);
            Assert.Greater(bend, 50f);
        }

        [Test]
        public void Wrist_LargeYaw_ClampedToDeviation()
        {
            var r = WristLimit.Clamp(Vector3.forward, Quaternion.Euler(0f, 80f, 0f), 55f, 32f);
            var bend = WristLimit.BendAngle(Vector3.forward, r);
            Assert.Less(bend, 33f);
            Assert.Greater(bend, 30f);
        }

        [Test]
        public void TwoBone_ReachableTarget_KeepsBoneLengths()
        {
            var s = new Vector3(0f, 0f, 0f);
            var w = new Vector3(0.1f, -0.05f, 0.4f);
            var r = TwoBoneSolver.Solve(s, w, Vector3.down, 0.29f, 0.27f);
            Assert.IsFalse(r.ShoulderShifted);
            Assert.AreEqual(0.29f, (r.Elbow - r.Shoulder).magnitude, 0.001f);
            Assert.AreEqual(0.27f, (w - r.Elbow).magnitude, 0.001f);
        }

        [Test]
        public void TwoBone_TooFar_ShiftsShoulderSoWristStaysOnTarget()
        {
            var w = new Vector3(0f, 0f, 1.2f);
            var r = TwoBoneSolver.Solve(Vector3.zero, w, Vector3.down, 0.29f, 0.27f);
            Assert.IsTrue(r.ShoulderShifted);
            Assert.AreEqual(0.27f, (w - r.Elbow).magnitude, 0.002f);
            Assert.Greater(r.Extension, 0.98f);
        }

        [Test]
        public void TwoBone_PoleDecidesElbowSide()
        {
            var w = new Vector3(0f, 0f, 0.4f);
            var down = TwoBoneSolver.Solve(Vector3.zero, w, Vector3.down, 0.29f, 0.27f);
            var up = TwoBoneSolver.Solve(Vector3.zero, w, Vector3.up, 0.29f, 0.27f);
            Assert.Less(down.Elbow.y, 0f);
            Assert.Greater(up.Elbow.y, 0f);
        }

        [Test]
        public void SupportHand_PistolNoSlide_RifleSlidesBack()
        {
            Assert.AreEqual(0f, SupportHand.AdsShift(GripClass.Pistol).z, 0.0001f);
            Assert.Less(SupportHand.AdsShift(GripClass.Rifle).z, -0.02f);
            Assert.Less(SupportHand.AdsShift(GripClass.Sniper).z, SupportHand.AdsShift(GripClass.Lmg).z);
        }

        [Test]
        public void Pole_TucksInwardWhenAiming()
        {
            var basePole = new Vector3(0.6f, -1f, -0.2f).normalized;
            var hip = SupportHand.AdaptPole(basePole, false, 0f);
            var ads = SupportHand.AdaptPole(basePole, false, 1f);
            Assert.Less(ads.x, hip.x);
            Assert.AreEqual(1f, ads.magnitude, 0.001f);
            var left = SupportHand.AdaptPole(new Vector3(-0.6f, -1f, -0.2f).normalized, true, 1f);
            Assert.Less(left.x, 0f);
        }

        [Test]
        public void Framing_ClassFovOrdering_AndClamp()
        {
            Assert.Greater(ViewmodelFraming.ClassFov(60f, GripClass.Pistol), ViewmodelFraming.ClassFov(60f, GripClass.Rifle));
            Assert.AreEqual(90f, ViewmodelFraming.ClassFov(89f, GripClass.Pistol), 0.001f);
            Assert.AreEqual(60f, ViewmodelFraming.ClassFov(60f, GripClass.None), 0.001f);
        }

        [Test]
        public void Framing_CoverageShrinksWithDepthAndFov()
        {
            var near = ViewmodelFraming.HalfWidthFraction(0.2f, 0.4f, 60f, 16f / 9f);
            var far = ViewmodelFraming.HalfWidthFraction(0.2f, 0.8f, 60f, 16f / 9f);
            var wide = ViewmodelFraming.HalfWidthFraction(0.2f, 0.4f, 80f, 16f / 9f);
            Assert.Greater(near, far);
            Assert.Greater(near, wide);
            Assert.AreEqual(near / 2f, far, 0.001f);
        }

        [Test]
        public void Framing_DepthForFraction_RoundTrips()
        {
            var d = ViewmodelFraming.DepthForFraction(0.3f, 0.4f, 60f, 16f / 9f);
            Assert.AreEqual(0.4f, ViewmodelFraming.HalfWidthFraction(0.3f, d, 60f, 16f / 9f), 0.001f);
        }

        [Test]
        public void Framing_PullBack_OnlyWhenTooLarge()
        {
            Assert.AreEqual(0f, ViewmodelFraming.PullBack(0.05f, 0.5f, 60f, 16f / 9f, GripClass.Rifle), 0.0001f);
            Assert.Greater(ViewmodelFraming.PullBack(0.6f, 0.3f, 60f, 16f / 9f, GripClass.Rifle), 0.05f);
        }
    }
}
