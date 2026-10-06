#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class RagdollMathTests
    {
        [Test]
        public void MassFractions_SumToOne()
        {
            Assert.IsTrue(Mathf.Abs(RagdollMath.MassFractionSum() - 1f) < 0.01f);
        }

        [Test]
        public void Specs_FormTreeRootedAtHips_AndLimitsAreSane()
        {
            Assert.AreEqual(RagdollMath.PartCount, RagdollMath.Specs.Length);
            for (var i = 0; i < RagdollMath.Specs.Length; i++)
            {
                var s = RagdollMath.Specs[i];
                Assert.AreEqual(i, (int)s.Part);
                if (i == 0)
                {
                    Assert.AreEqual(-1, s.Parent);
                    continue;
                }

                Assert.IsTrue(s.Parent >= 0 && s.Parent < i);
                if (s.Kind == RagdollJointKind.Ball)
                {
                    Assert.IsTrue(s.TwistLow <= 0f && s.TwistHigh >= 0f);
                    Assert.IsTrue(s.Swing1 > 0f && s.Swing1 <= 177f);
                    Assert.IsTrue(s.Swing2 > 0f && s.Swing2 <= 177f);
                }
                else
                {
                    Assert.IsTrue(s.HingeMin < s.HingeMax);
                    Assert.IsTrue(s.HingeMax - s.HingeMin <= 170f);
                }
            }
        }

        [Test]
        public void Knees_BendBackward_Elbows_BendForward()
        {
            Assert.IsTrue(RagdollMath.Specs[(int)RagdollPart.ShinL].HingeMin >= 0f);
            Assert.IsTrue(RagdollMath.Specs[(int)RagdollPart.ForearmL].HingeMax <= 0f);
        }

        [Test]
        public void HingeRelativeLimits_AlwaysContainZero()
        {
            float min, max;
            RagdollMath.HingeRelativeLimits(0f, 140f, 60f, out min, out max);
            Assert.AreEqual(-60f, min, 0.001f);
            Assert.AreEqual(80f, max, 0.001f);
            RagdollMath.HingeRelativeLimits(0f, 140f, -10f, out min, out max);
            Assert.AreEqual(0f, min, 0.001f);
            Assert.IsTrue(max > 0f);
            RagdollMath.HingeRelativeLimits(-145f, 0f, -70f, out min, out max);
            Assert.AreEqual(-75f, min, 0.001f);
            Assert.AreEqual(70f, max, 0.001f);
        }

        [Test]
        public void MaxActive_PerTier()
        {
            Assert.AreEqual(2, RagdollMath.MaxActive(0));
            Assert.AreEqual(6, RagdollMath.MaxActive(1));
            Assert.AreEqual(12, RagdollMath.MaxActive(2));
            Assert.AreEqual(20, RagdollMath.MaxActive(3));
            Assert.AreEqual(2, RagdollMath.MaxActive(-5));
            Assert.AreEqual(20, RagdollMath.MaxActive(99));
        }

        [Test]
        public void LowTier_DisablesSpineAndForearms()
        {
            Assert.IsFalse(RagdollMath.PartEnabled(RagdollPart.Spine, 0));
            Assert.IsFalse(RagdollMath.PartEnabled(RagdollPart.ForearmL, 0));
            Assert.IsTrue(RagdollMath.PartEnabled(RagdollPart.Chest, 0));
            Assert.IsTrue(RagdollMath.PartEnabled(RagdollPart.Spine, 1));
        }

        [Test]
        public void Impulse_HeavierCaliberAndExplosionsPushMore()
        {
            var pistol = RagdollMath.ImpulseNs("pistol_sar9", 30f, false);
            var rifle = RagdollMath.ImpulseNs("ar_mpt76", 30f, false);
            var sniper = RagdollMath.ImpulseNs("sr_jng90", 30f, false);
            var frag = RagdollMath.ImpulseNs("grenade_frag", 60f, false);
            Assert.IsTrue(pistol < rifle);
            Assert.IsTrue(rifle < sniper);
            Assert.IsTrue(sniper < frag);
            Assert.IsTrue(RagdollMath.IsExplosive("grenade_frag"));
            Assert.IsFalse(RagdollMath.IsExplosive("ar_mpt55"));
            Assert.IsTrue(RagdollMath.ImpulseNs("zone", 10f, false) < 1f);
            Assert.IsTrue(RagdollMath.ImpulseNs("ar_mpt55", 30f, true) > RagdollMath.ImpulseNs("ar_mpt55", 30f, false));
        }

        [Test]
        public void Kick_IsClampedAndPointsAlongHit()
        {
            var k = RagdollMath.ComputeKick(Vector3.forward, 1000f, true, 4f);
            Assert.IsTrue(k.SharedDeltaV.magnitude <= RagdollMath.MaxSharedSpeed + 0.001f);
            Assert.IsTrue(k.HitPartDeltaV.magnitude <= RagdollMath.MaxHitSpeed + 0.001f);
            Assert.IsTrue(k.Spin.magnitude <= RagdollMath.MaxSpin + 0.001f);
            Assert.IsTrue(k.SharedDeltaV.y > 0f, "patlama yukarı savurur");

            var b = RagdollMath.ComputeKick(Vector3.forward, 7f, false, 15f);
            Assert.IsTrue(Vector3.Dot(b.HitPartDeltaV.normalized, Vector3.forward) > 0.9f);
            Assert.IsTrue(b.HitPartDeltaV.magnitude > 0.3f);
        }

        [Test]
        public void TotalMass_Is80Kg_AndPartsRealistic()
        {
            Assert.AreEqual(80f, RagdollMath.TotalMass, 0.001f);
            Assert.AreEqual(1f, RagdollMath.MassFractionSum(), 0.005f);
            var trunk = RagdollMath.MassOf(RagdollPart.Hips) + RagdollMath.MassOf(RagdollPart.Spine) + RagdollMath.MassOf(RagdollPart.Chest);
            Assert.IsTrue(trunk > 36f && trunk < 44f);
            Assert.IsTrue(RagdollMath.MassOf(RagdollPart.Head) > 4f && RagdollMath.MassOf(RagdollPart.Head) < 7f);
            Assert.IsTrue(RagdollMath.MassOf(RagdollPart.ThighL) > RagdollMath.MassOf(RagdollPart.ShinL));
        }

        [Test]
        public void NetImpulse_IsCapped_RifleCrumplesNotLaunches()
        {
            Assert.IsTrue(RagdollMath.NetImpulse(1000f, false) <= RagdollMath.MaxNetImpulseNs + 0.001f);
            Assert.IsTrue(RagdollMath.NetImpulse(1000f, true) <= RagdollMath.MaxExplosionImpulseNs + 0.001f);
            Assert.IsTrue(RagdollMath.MaxNetImpulseNs <= 250f);
            var sniperHs = RagdollMath.ImpulseNs("sr_jng90", 90f, true);
            var k = RagdollMath.ComputeKick(Vector3.forward, sniperHs, false, 17.6f, true);
            Assert.IsTrue(k.SharedDeltaV.magnitude < 2.5f);
            Assert.IsTrue(k.HitPartDeltaV.magnitude <= RagdollMath.MaxHitSpeed + 0.001f);
        }

        [Test]
        public void ExplosionLoft_IsCapped()
        {
            var k = RagdollMath.ComputeKick(Vector3.up, 100000f, true, 1f);
            Assert.IsTrue(k.SharedDeltaV.y <= RagdollMath.MaxSharedLoft + 0.001f);
            Assert.IsTrue(k.HitPartDeltaV.y <= RagdollMath.MaxHitLoft + 0.001f);
            var peak = (k.SharedDeltaV.y + k.HitPartDeltaV.y);
            Assert.IsTrue(peak * peak / (2f * 9.81f) < 4f, "20 m uçuş yok");
        }

        [Test]
        public void HeadshotSnap_AddsSpin_ButBounded()
        {
            var a = RagdollMath.ComputeKick(Vector3.forward, 7f, false, 5.6f, false);
            var b = RagdollMath.ComputeKick(Vector3.forward, 7f, false, 5.6f, true);
            Assert.IsTrue(b.Spin.magnitude > a.Spin.magnitude);
            Assert.IsTrue(b.Spin.magnitude <= RagdollMath.MaxSpin + 0.001f);
        }

        [Test]
        public void JointLimits_PreventOwlHead_AndBackwardKnees()
        {
            var head = RagdollMath.Specs[(int)RagdollPart.Head];
            Assert.IsTrue(head.TwistHigh <= 35f && head.Swing1 <= 40f && head.Swing2 <= 30f);
            for (var i = 0; i < 2; i++)
            {
                var knee = RagdollMath.Specs[(int)RagdollPart.ShinL + i];
                Assert.AreEqual(0f, knee.HingeMin, 0f);
                Assert.IsTrue(knee.HingeMax <= 140f);
                var elbow = RagdollMath.Specs[(int)RagdollPart.ForearmL + i];
                Assert.AreEqual(0f, elbow.HingeMax, 0f);
                Assert.IsTrue(elbow.HingeMin >= -145f);
            }

            var hip = RagdollMath.Specs[(int)RagdollPart.ThighL];
            Assert.IsTrue(hip.Swing1 <= 80f && hip.TwistHigh <= 20f);
        }

        [Test]
        public void WeaponAndHelmetVelocity_AreBounded()
        {
            var w = RagdollMath.WeaponVelocity(new Vector3(0f, 0f, 6f), new Vector3(0f, 0f, 3f));
            Assert.IsTrue(w.z > 6f * 0.8f);
            var big = RagdollMath.WeaponVelocity(Vector3.forward * 100f, Vector3.forward * 100f);
            Assert.IsTrue(big.magnitude <= RagdollMath.MaxSharedSpeed + 6f + 0.001f);
            var h = RagdollMath.HelmetVelocity(Vector3.zero, Vector3.zero);
            Assert.IsFalse(float.IsNaN(h.x));
            Assert.IsTrue(h.y > 0f && h.magnitude < 5f);
            Assert.IsTrue(RagdollMath.BodyDynamicFriction >= 0.6f);
        }

        [Test]
        public void Kick_ZeroDirection_FallsBackWithoutNaN()
        {
            var k = RagdollMath.ComputeKick(Vector3.zero, 5f, false, 10f);
            Assert.IsFalse(float.IsNaN(k.HitPartDeltaV.x) || float.IsNaN(k.Spin.x));
        }

        [Test]
        public void PartForRegion_MapsBodyParts()
        {
            Assert.AreEqual(RagdollPart.Head, RagdollMath.PartForRegion((int)BodyPart.Head, true));
            Assert.AreEqual(RagdollPart.Chest, RagdollMath.PartForRegion((int)BodyPart.Torso, true));
            Assert.AreEqual(RagdollPart.UpperArmR, RagdollMath.PartForRegion((int)BodyPart.Arm, false));
            Assert.AreEqual(RagdollPart.ThighL, RagdollMath.PartForRegion((int)BodyPart.Leg, true));
        }

        [Test]
        public void Settle_FreezesWhenCalm_AndOnTimeout()
        {
            var s = new RagdollSettle();
            var frozen = false;
            for (var t = 0f; t < 3f && !frozen; t += 0.1f)
                frozen = s.Tick(0.1f, 0.01f, 0.01f);
            Assert.IsTrue(frozen);
            Assert.IsTrue(s.Elapsed >= 1.2f);

            s.Reset();
            frozen = false;
            var time = 0f;
            while (!frozen && time < 10f)
            {
                frozen = s.Tick(0.1f, 3f, 5f);
                time += 0.1f;
            }

            Assert.IsTrue(frozen);
            Assert.IsTrue(time >= 5.9f && time <= 6.2f);
        }

        [Test]
        public void HitSpring_ReturnsToRest_AndStaysBounded()
        {
            var sp = new HitReactionSpring();
            HitReactionMath.Apply(sp, BodyPart.Head, Vector3.back, 1f, 3, true);
            Assert.IsTrue(sp.Active);
            var peak = 0f;
            for (var i = 0; i < 20; i++)
            {
                sp.Step(0.016f);
                peak = Mathf.Max(peak, Mathf.Abs(sp.Get(HitChannel.HeadPitch)));
            }

            Assert.IsTrue(peak > 3f && peak <= 28f);
            for (var i = 0; i < 300; i++)
                sp.Step(0.016f);
            Assert.IsFalse(sp.Active);
            Assert.AreEqual(0f, sp.Get(HitChannel.HeadPitch), 0.001f);
        }

        [Test]
        public void HitReaction_FrontHeadshot_SnapsHeadBack_LegHitBuckles_TierGates()
        {
            var sp = new HitReactionSpring();
            // Önden vuruş: mermi -Z yönünde gider; baş geriye (negatif pitch) kırılır.
            HitReactionMath.Apply(sp, BodyPart.Head, Vector3.back, 1f, 2, false);
            sp.Step(0.03f);
            Assert.IsTrue(sp.Get(HitChannel.HeadPitch) < 0f);

            var leg = new HitReactionSpring();
            HitReactionMath.Apply(leg, BodyPart.Leg, Vector3.back, 1f, 2, false);
            leg.Step(0.05f);
            Assert.IsTrue(leg.Get(HitChannel.LegBuckle) > 0f);

            var low = new HitReactionSpring();
            HitReactionMath.Apply(low, BodyPart.Leg, Vector3.back, 1f, 0, true);
            low.Step(0.05f);
            Assert.AreEqual(0f, low.Get(HitChannel.LegBuckle), 1e-5f);
            Assert.AreEqual(0f, low.Get(HitChannel.StaggerZ), 1e-5f);

            var heavy = new HitReactionSpring();
            HitReactionMath.Apply(heavy, BodyPart.Torso, Vector3.forward, 1f, 3, true);
            heavy.Step(0.05f);
            Assert.IsTrue(heavy.Get(HitChannel.StaggerZ) > 0f);
        }
    }
}
#endif
