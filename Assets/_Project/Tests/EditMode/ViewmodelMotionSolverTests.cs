using NUnit.Framework;
using Project.Application.Services;
using Project.Application.Viewmodel;

namespace Project.Tests.EditMode
{
    public sealed class ViewmodelMotionSolverTests
    {
        private static float MaxAbs(float a, float b) => System.Math.Abs(a) > b ? System.Math.Abs(a) : b;

        [Test]
        public void SpringImpulse_ProducesRequestedPeak()
        {
            var x = 0f; var v = SpringImpulse.VelocityForPeak(0.05f, 1600f, 0.5f);
            var peak = 0f;
            for (var i = 0; i < 2000; i++)
            {
                ViewmodelDynamics.StepSpring(ref x, ref v, 0f, 1600f, 0.5f, 0.0005f);
                peak = MaxAbs(x, peak);
            }
            Assert.AreEqual(0.05f, peak, 0.002f);
        }

        [Test]
        public void SpringImpulse_CriticalPeak()
        {
            var x = 0f; var v = SpringImpulse.VelocityForPeak(0.03f, 400f, 1f);
            var peak = 0f;
            for (var i = 0; i < 2000; i++)
            {
                ViewmodelDynamics.StepSpring(ref x, ref v, 0f, 400f, 1f, 0.0005f);
                peak = MaxAbs(x, peak);
            }
            Assert.AreEqual(0.03f, peak, 0.0015f);
        }

        [Test]
        public void Recoil_PeaksThenRecovers()
        {
            var r = new ProceduralWeaponRecoil();
            r.SetTuning(ViewmodelTuning.Recoil(ViewmodelWeaponClass.Rifle));
            r.Fire(0f, 0f, 0f, out var cp, out var cy);
            var peak = 0f;
            for (var i = 0; i < 120; i++) { r.Step(1f / 240f); peak = MaxAbs(r.PitchDeg, peak); }
            Assert.IsTrue(peak > 0.5f, "kalkis " + peak);
            for (var i = 0; i < 480; i++) r.Step(1f / 240f);
            Assert.IsTrue(System.Math.Abs(r.PitchDeg) < 0.02f);
            Assert.IsTrue(System.Math.Abs(r.KickbackM) < 0.0005f);
            Assert.Greater(cp, 0f);
        }

        [Test]
        public void Recoil_SplitConservesTotal()
        {
            ProceduralWeaponRecoil.SplitRise(4f, 0.4f, out var w, out var c);
            Assert.AreEqual(4f, w + c, 0.0001f);
            Assert.AreEqual(1.6f, c, 0.0001f);
        }

        [Test]
        public void Recoil_AdsReducesKick()
        {
            var a = new ProceduralWeaponRecoil(); var b = new ProceduralWeaponRecoil();
            a.SetTuning(ViewmodelTuning.Recoil(ViewmodelWeaponClass.Rifle));
            b.SetTuning(ViewmodelTuning.Recoil(ViewmodelWeaponClass.Rifle));
            a.Fire(0f, 0f, 0f, out _, out _); b.Fire(0f, 0f, 1f, out _, out _);
            float pa = 0f, pb = 0f;
            for (var i = 0; i < 100; i++) { a.Step(0.002f); b.Step(0.002f); pa = MaxAbs(a.KickbackM, pa); pb = MaxAbs(b.KickbackM, pb); }
            Assert.Less(pb, pa);
        }

        [Test]
        public void Recoil_BurstGainCapped()
        {
            Assert.AreEqual(1f, ProceduralWeaponRecoil.BurstGain(0), 0.0001f);
            Assert.AreEqual(ProceduralWeaponRecoil.MaxBurstGain, ProceduralWeaponRecoil.BurstGain(50), 0.0001f);
        }

        [Test]
        public void StepBob_PhaseFollowsDistanceNotTime()
        {
            var a = new StepSyncedBob(); var b = new StepSyncedBob();
            for (var i = 0; i < 100; i++) a.Step(0.03f, 1.9f, true, 0.016f);
            for (var i = 0; i < 50; i++) b.Step(0.06f, 1.9f, true, 0.032f);
            Assert.AreEqual(a.Phase, b.Phase, 0.001f);
        }

        [Test]
        public void StepBob_FootfallEveryHalfStride()
        {
            var bob = new StepSyncedBob();
            var steps = 0;
            for (var i = 0; i < 1000; i++) { bob.Step(0.019f, 1.9f, true, 0.01f); steps += bob.FootfallsThisStep; }
            // 19 m / 1.9 m = 10 döngü = 20 adım.
            Assert.IsTrue(steps >= 19 && steps <= 20, "adim " + steps);
        }

        [Test]
        public void StepBob_StopsWhenIdleAndAirborne()
        {
            var bob = new StepSyncedBob();
            for (var i = 0; i < 60; i++) bob.Step(0.02f, 1.9f, true, 0.016f);
            Assert.Greater(bob.Amplitude, 0.5f);
            var ph = bob.Phase;
            for (var i = 0; i < 200; i++) bob.Step(0f, 1.9f, true, 0.016f);
            Assert.Less(bob.Amplitude, 0.01f);
            Assert.AreEqual(ph, bob.Phase, 0.0001f);
        }

        [Test]
        public void Ads_EnterIsMonotonicAndExitFaster()
        {
            var a = new AdsCurveBlend();
            var prev = 0f;
            var tEnter = 0f;
            while (!a.IsFull && tEnter < 2f) { a.Step(true, 0.2f, 0.005f); tEnter += 0.005f; Assert.IsTrue(a.Weight >= prev - 0.0001f); prev = a.Weight; }
            var tExit = 0f;
            while (a.Linear > 0f && tExit < 2f) { a.Step(false, 0.2f, 0.005f); tExit += 0.005f; }
            Assert.Less(tExit, tEnter);
            Assert.AreEqual(0f, a.Weight, 0.0001f);
        }

        [Test]
        public void Ads_CurveEndpoints()
        {
            Assert.AreEqual(0f, AdsCurveBlend.EnterCurve(0f), 0.0001f);
            Assert.AreEqual(1f, AdsCurveBlend.EnterCurve(1f), 0.0001f);
            Assert.AreEqual(0.5f, AdsCurveBlend.EnterCurve(0.5f), 0.0001f);
        }

        [Test]
        public void Landing_ScalesWithFallSpeed()
        {
            Assert.AreEqual(0f, LandingDip.DepthFor(2f), 0.0001f);
            Assert.Less(LandingDip.DepthFor(5f), LandingDip.DepthFor(12f));
            Assert.AreEqual(LandingDip.MaxDepthM, LandingDip.DepthFor(40f), 0.0001f);
        }

        [Test]
        public void Landing_DipsDownThenSettles()
        {
            var l = new LandingDip();
            l.Land(10f, 1f);
            var min = 0f;
            for (var i = 0; i < 200; i++) { l.Step(0.005f); if (l.OffsetY < min) min = l.OffsetY; }
            Assert.IsTrue(min < -0.01f, "cokme " + min);
            for (var i = 0; i < 400; i++) l.Step(0.005f);
            Assert.IsTrue(System.Math.Abs(l.OffsetY) < 0.0005f);
        }

        [Test]
        public void Sway_LagsOppositeToLook()
        {
            var s = new InertiaSway();
            for (var i = 0; i < 30; i++) s.Step(2f, 0f, 0f, 1f, 0.01f);
            Assert.Less(s.YawDeg, -0.5f);
            Assert.IsTrue(s.YawDeg >= -ViewmodelMotionMath.MaxTurnLagDeg * 1.4f);
        }

        [Test]
        public void Sway_ReducedInAds()
        {
            var a = new InertiaSway(); var b = new InertiaSway();
            for (var i = 0; i < 30; i++) { a.Step(2f, 0f, 0f, 1f, 0.01f); b.Step(2f, 0f, 0f, 0.35f, 0.01f); }
            Assert.Less(System.Math.Abs(b.YawDeg), System.Math.Abs(a.YawDeg));
        }

        [Test]
        public void Tuning_AdsTimesOrdered()
        {
            Assert.Less(ViewmodelTuning.AdsSeconds(ViewmodelWeaponClass.Pistol), ViewmodelTuning.AdsSeconds(ViewmodelWeaponClass.Rifle));
            Assert.Less(ViewmodelTuning.AdsSeconds(ViewmodelWeaponClass.Rifle), ViewmodelTuning.AdsSeconds(ViewmodelWeaponClass.Sniper));
        }

        [Test]
        public void Solver_OutputsBoundedUnderStress()
        {
            var s = new ViewmodelMotionSolver();
            var inp = new ViewmodelMotionInput { WeaponClass = ViewmodelWeaponClass.Rifle, Grounded = true, Sprinting = true, Distance = 0.08f, LookYawDelta = 30f, LookPitchDelta = -10f, Shots = 1, RandomSide = 1f, RandomRoll = -1f };
            for (var i = 0; i < 600; i++)
            {
                var o = s.Step(inp, 1f / 120f);
                Assert.IsTrue(!float.IsNaN(o.PosX) && !float.IsNaN(o.Pitch) && !float.IsNaN(o.CameraPitch));
                Assert.IsTrue(System.Math.Abs(o.Pitch) < 80f, "pitch " + o.Pitch);
                Assert.IsTrue(System.Math.Abs(o.PosZ) < 0.3f);
            }
        }

        [Test]
        public void Solver_SprintBlendsInAndOut()
        {
            var s = new ViewmodelMotionSolver();
            var inp = new ViewmodelMotionInput { WeaponClass = ViewmodelWeaponClass.Rifle, Grounded = true, Sprinting = true, Distance = 0.08f };
            ViewmodelMotionOutput o = default;
            for (var i = 0; i < 120; i++) o = s.Step(inp, 1f / 120f);
            Assert.Greater(o.SprintWeight, 0.9f);
            inp.Sprinting = false; inp.Distance = 0f;
            for (var i = 0; i < 120; i++) o = s.Step(inp, 1f / 120f);
            Assert.Less(o.SprintWeight, 0.05f);
        }

        [Test]
        public void Solver_AdsOverridesSprintAndNarrowsFov()
        {
            var s = new ViewmodelMotionSolver();
            var inp = new ViewmodelMotionInput { WeaponClass = ViewmodelWeaponClass.Smg, Grounded = true, Sprinting = true, Aiming = true };
            ViewmodelMotionOutput o = default;
            for (var i = 0; i < 120; i++) o = s.Step(inp, 1f / 120f);
            Assert.AreEqual(1f, o.AdsWeight, 0.001f);
            Assert.Less(o.FovScale, 0.95f);
            Assert.Less(o.SprintWeight, 0.05f);
        }
    }
}
