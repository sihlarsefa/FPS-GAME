using Project.Core.Domain;
using Project.Infrastructure.Characters.WeaponHold;
using UnityEngine;
using NUnit.Framework;

namespace Project.Tests.EditMode
{
    public sealed class WeaponHoldTests
    {
        private static HoldInput Input(float pitch, WeaponCategory c)
        {
            return new HoldInput { Pitch = pitch, Profile = WeaponHoldProfile.ForCategory(c) };
        }

        [Test]
        public void Evaluate_DistributesPitchAcrossBody()
        {
            var o = WeaponHoldMath.Evaluate(Input(40f, WeaponCategory.AssaultRifle));
            Assert.AreEqual(40f, o.ArmsPitch, 0.01f);
            Assert.IsTrue(o.SpineAim > 8f && o.SpineAim < 14f);
            Assert.IsTrue(o.ChestAim > 8f && o.ChestAim < 14f);
            Assert.IsTrue(o.PelvisPitch < 0f);
            Assert.AreEqual(36f, o.HeadPitch, 0.5f);
        }

        [Test]
        public void HeavyWeapon_BendsSpineLessThanSmg()
        {
            var lmg = WeaponHoldMath.Evaluate(Input(40f, WeaponCategory.Lmg));
            var smg = WeaponHoldMath.Evaluate(Input(40f, WeaponCategory.Smg));
            Assert.Less(lmg.SpineAim, smg.SpineAim);
        }

        [Test]
        public void Sprint_DropsMuzzleAndYaws()
        {
            var i = Input(0f, WeaponCategory.AssaultRifle);
            i.Run = 1f;
            var o = WeaponHoldMath.Evaluate(i);
            Assert.AreEqual(30f, o.ArmsPitch, 0.01f);
            Assert.AreEqual(25f, o.ArmsYaw, 0.01f);
        }

        [Test]
        public void LowReady_LowersMuzzleAndSocket_ButSprintOverrides()
        {
            var i = Input(0f, WeaponCategory.AssaultRifle);
            i.LowReady = 1f;
            var o = WeaponHoldMath.Evaluate(i);
            Assert.AreEqual(22f, o.ArmsPitch, 0.01f);
            Assert.Less(o.SocketOffset.y, 0f);
            Assert.Less(o.SocketOffset.z, 0f);
            i.Run = 1f;
            var r = WeaponHoldMath.Evaluate(i);
            Assert.AreEqual(30f, r.ArmsPitch, 0.01f);
        }

        [Test]
        public void WallPull_RaisesMuzzleAndPullsBack()
        {
            var i = Input(0f, WeaponCategory.AssaultRifle);
            i.WallPull = 1f;
            var o = WeaponHoldMath.Evaluate(i);
            Assert.Less(o.ArmsPitch, 0f);
            Assert.Less(o.SocketOffset.z, 0f);
        }

        [Test]
        public void WallPullTarget_Curve()
        {
            Assert.AreEqual(0f, WeaponHoldMath.WallPullTarget(2f, 0.85f), 0.001f);
            Assert.AreEqual(1f, WeaponHoldMath.WallPullTarget(0.1f, 0.85f), 0.001f);
            var mid = WeaponHoldMath.WallPullTarget(0.6f, 0.85f);
            Assert.IsTrue(mid > 0.1f && mid < 0.9f);
            Assert.AreEqual(0f, WeaponHoldMath.WallPullTarget(float.PositiveInfinity, 0.85f), 0.001f);
        }

        [Test]
        public void RecoilShape_PeaksAndDecaysMonotonic()
        {
            Assert.AreEqual(1f, WeaponHoldMath.RecoilShape(1f), 0.001f);
            Assert.AreEqual(0f, WeaponHoldMath.RecoilShape(0f), 0.001f);
            Assert.Less(WeaponHoldMath.RecoilShape(0.5f), 0.5f);
            Assert.AreEqual(1f, WeaponHoldMath.RecoilShape(5f), 0.001f);
        }

        [Test]
        public void ResolveWristReach_LeavesReachableWristAlone()
        {
            var w = new Vector3(-0.03f, 0f, 0.3f);
            var r = WeaponHoldMath.ResolveWristReach(Vector3.zero, Vector3.forward, w, new Vector3(-0.2f, 0f, 0f), 0.524f, 0.06f);
            Assert.AreEqual(w.z, r.z, 0.0001f);
        }

        [Test]
        public void ResolveWristReach_PullsBackIntoReach()
        {
            var shoulder = new Vector3(-0.2f, 0f, 0f);
            var w = new Vector3(-0.03f, 0f, 0.8f);
            var r = WeaponHoldMath.ResolveWristReach(Vector3.zero, Vector3.forward, w, shoulder, 0.524f, 0.06f);
            Assert.Less(r.z, 0.8f);
            Assert.IsTrue((r - shoulder).magnitude < 0.524f + 0.002f);
            Assert.IsTrue(r.z >= 0.06f - 0.0001f);
        }

        [Test]
        public void ResolveWristReach_NeverRetreatsBelowFloor()
        {
            var r = WeaponHoldMath.ResolveWristReach(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f), Vector3.zero, 0.01f, 0.06f);
            Assert.AreEqual(0.06f, r.z, 0.001f);
        }

        [Test]
        public void AlignHand_ClampsDeviationFromLegacy()
        {
            var legacy = Quaternion.Euler(-20f, 0f, 0f);
            var r = WeaponHoldMath.AlignHand(Quaternion.identity, Quaternion.Euler(120f, 0f, 0f), Quaternion.identity, legacy);
            Assert.IsTrue(Quaternion.Angle(legacy, r) <= WeaponHoldMath.HandMaxDeviationDegrees + 0.1f);
            var near = WeaponHoldMath.AlignHand(Quaternion.identity, Quaternion.Euler(-30f, 0f, 0f), Quaternion.identity, legacy);
            Assert.IsTrue(Quaternion.Angle(near, Quaternion.Euler(-30f, 0f, 0f)) < 0.1f);
        }

        [Test]
        public void GripFrame_PointsBoneAxisAlongBarrel()
        {
            var barrel = new Vector3(0.2f, -0.4f, 0.9f).normalized;
            var q = WeaponHoldMath.GripFrame(barrel, 0f);
            Assert.AreEqual(1f, Vector3.Dot(q * Vector3.down, barrel), 0.01f);
        }

        [Test]
        public void Spring_ConvergesAndLagsHeavyMore()
        {
            var heavy = new WeaponSpring();
            var light = new WeaponSpring();
            heavy.Step(0.05f, 30f, 4f, 0.9f);
            light.Step(0.05f, 30f, 9f, 0.75f);
            Assert.Less(heavy.Value, light.Value);
            Assert.Less(light.Value, 45f);
            for (var i = 0; i < 200; i++)
                heavy.Step(0.02f, 30f, 4f, 0.9f);
            Assert.AreEqual(30f, heavy.Value, 0.1f);
        }

        [Test]
        public void Spring_IgnoresNaN()
        {
            var s = new WeaponSpring();
            s.Reset(5f);
            s.Step(0.02f, float.NaN, 5f, 1f);
            Assert.AreEqual(5f, s.Value, 0.0001f);
        }

        [Test]
        public void ReadyState_LowersAfterIdleAndRaisesOnActivity()
        {
            var r = new WeaponReadyState();
            for (var i = 0; i < 100; i++)
                r.Update(0.1f, 0f, 5f, float.PositiveInfinity, 0.85f, true);
            Assert.AreEqual(1f, r.LowReady, 0.001f);
            r.NotifyActivity();
            for (var i = 0; i < 5; i++)
                r.Update(0.1f, 0f, 5f, float.PositiveInfinity, 0.85f, true);
            Assert.AreEqual(0f, r.LowReady, 0.001f);
        }

        [Test]
        public void ReadyState_MovementPreventsLowReady()
        {
            var r = new WeaponReadyState();
            for (var i = 0; i < 100; i++)
                r.Update(0.1f, 3f, 5f, float.PositiveInfinity, 0.85f, true);
            Assert.AreEqual(0f, r.LowReady, 0.001f);
        }

        [Test]
        public void ReadyState_WallPullRisesNearObstacle()
        {
            var r = new WeaponReadyState();
            for (var i = 0; i < 40; i++)
                r.Update(0.05f, 0f, 5f, 0.2f, 0.85f, true);
            Assert.Greater(r.WallPull, 0.95f);
            for (var i = 0; i < 100; i++)
                r.Update(0.05f, 0f, 5f, float.PositiveInfinity, 0.85f, true);
            Assert.Less(r.WallPull, 0.01f);
        }
    }
}
