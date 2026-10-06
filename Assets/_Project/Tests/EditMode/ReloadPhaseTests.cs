using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Infrastructure.Weapons.Reload;

namespace Project.Tests.EditMode
{
    public sealed class ReloadPhaseTests
    {
        private static readonly ReloadKind[] Kinds =
            { ReloadKind.Rifle, ReloadKind.Pistol, ReloadKind.BoltAction, ReloadKind.Machinegun, ReloadKind.Shotgun };

        [Test]
        public void Plan_EndsMonotonic_AndSumToTotal()
        {
            foreach (var k in Kinds)
                foreach (var empty in new[] { false, true })
                {
                    var p = ReloadPhasePlan.Build(k, empty, 2.5f);
                    float prev = 0f, sum = 0f;
                    for (var i = 0; i < ReloadPhasePlan.PhaseCount; i++)
                    {
                        var ph = (ReloadPhase)i;
                        Assert.IsTrue(p.PhaseEnd(ph) >= prev);
                        prev = p.PhaseEnd(ph);
                        sum += p.PhaseSeconds(ph);
                    }
                    Assert.AreEqual(1f, prev, 0.0001f);
                    Assert.AreEqual(2.5f, sum, 0.001f);
                }
        }

        [Test]
        public void Plan_AlignsWithPoseTimelineConstants()
        {
            var r = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2f);
            Assert.AreEqual(ViewmodelPoseTimeline.RifleMagOut, r.MagOutTime, 0.0001f);
            Assert.AreEqual(ViewmodelPoseTimeline.RifleMagIn, r.SeatTime, 0.0001f);
            var p = ReloadPhasePlan.Build(ReloadKind.Pistol, true, 1.5f);
            Assert.AreEqual(ViewmodelPoseTimeline.PistolMagIn, p.SeatTime, 0.0001f);
        }

        [Test]
        public void TacticalHasNoChamberPhase_EmptyHas()
        {
            Assert.IsFalse(ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2f).HasChamberPhase);
            Assert.IsTrue(ReloadPhasePlan.Build(ReloadKind.Rifle, true, 2f).HasChamberPhase);
            Assert.IsTrue(ReloadPhasePlan.Build(ReloadKind.BoltAction, false, 2f).HasChamberPhase);
        }

        [Test]
        public void TacticalIsFasterThanEmpty()
        {
            Assert.Less(ReloadPhasePlan.ReferenceSeconds(ReloadKind.Rifle, false), ReloadPhasePlan.ReferenceSeconds(ReloadKind.Rifle, true));
            var ratio = ReloadPhasePlan.TacticalRatio(ReloadKind.Rifle);
            Assert.Greater(ratio, 0.7f);
            Assert.Less(ratio, 0.85f);
        }

        [Test]
        public void PhaseAt_ReturnsExpectedPhases()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Rifle, true, 2.7f);
            Assert.AreEqual(ReloadPhase.Prep, p.PhaseAt(0.05f));
            Assert.AreEqual(ReloadPhase.MagRelease, p.PhaseAt(0.2f));
            Assert.AreEqual(ReloadPhase.Insert, p.PhaseAt(0.6f));
            Assert.AreEqual(ReloadPhase.Seat, p.PhaseAt(0.75f));
            Assert.AreEqual(ReloadPhase.Chamber, p.PhaseAt(0.85f));
            Assert.AreEqual(ReloadPhase.Recover, p.PhaseAt(0.99f));
            var tac = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2.1f);
            Assert.AreEqual(ReloadPhase.Recover, tac.PhaseAt(0.85f));
        }

        [Test]
        public void Tracker_EmitsEachPhaseOnce_EvenWhenSkipping()
        {
            var plan = ReloadPhasePlan.Build(ReloadKind.Rifle, true, 2.7f);
            var tr = new ReloadPhaseTracker(plan);
            var seen = new List<ReloadPhase>();
            var cues = new List<ReloadCue>();
            for (var t = 0f; t <= 1f; t += 0.3f)
                tr.Advance(t, seen.Add, cues.Add);
            tr.Advance(1f, seen.Add, cues.Add);
            var again = tr.Advance(1f, seen.Add, cues.Add);
            Assert.AreEqual(0, again);
            Assert.AreEqual(ReloadPhase.Prep, seen[0]);
            Assert.AreEqual(ReloadPhase.Recover, seen[seen.Count - 1]);
            for (var i = 1; i < seen.Count; i++)
                Assert.IsTrue((int)seen[i] > (int)seen[i - 1]);
            Assert.IsTrue(cues.Contains(ReloadCue.MagIn));
            Assert.IsTrue(cues.Contains(ReloadCue.BoltRelease));
        }

        [Test]
        public void Tracker_TacticalHasNoBoltReleaseCue()
        {
            var plan = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2.1f);
            var tr = new ReloadPhaseTracker(plan);
            var cues = new List<ReloadCue>();
            tr.Advance(0.5f, null, cues.Add);
            tr.Advance(1f, null, cues.Add);
            Assert.IsFalse(cues.Contains(ReloadCue.BoltRelease));
            Assert.IsTrue(cues.Contains(ReloadCue.MagRelease));
        }

        [Test]
        public void Cancel_BeforeMagOut_IsFree()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2.1f);
            var d = ReloadCancelRules.Evaluate(p, 0.05f, ReloadCancelReason.Sprint);
            Assert.IsTrue(d.Allowed);
            Assert.AreEqual(CancelClass.Free, d.Class);
            Assert.IsFalse(d.AmmoCommitted);
        }

        [Test]
        public void Cancel_MagOut_IsPenalty_AndBlocksFire()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2.1f);
            var d = ReloadCancelRules.Evaluate(p, 0.4f, ReloadCancelReason.Manual);
            Assert.AreEqual(CancelClass.Penalty, d.Class);
            Assert.IsFalse(d.AmmoCommitted);
            Assert.AreEqual(p.MagOutTime, d.ResumeFraction, 0.0001f);
            Assert.IsFalse(ReloadCancelRules.Evaluate(p, 0.4f, ReloadCancelReason.Fire).Allowed);
        }

        [Test]
        public void Cancel_TacticalAfterSeat_FireIsInstantCommit()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2.1f);
            var d = ReloadCancelRules.Evaluate(p, 0.70f, ReloadCancelReason.Fire);
            Assert.IsTrue(d.Allowed);
            Assert.IsTrue(d.AmmoCommitted);
            Assert.IsTrue(d.CanFireImmediately);
            Assert.AreEqual(0f, d.BlendSeconds, 0.0001f);
        }

        [Test]
        public void Cancel_EmptyBeforeBoltRelease_CannotFire()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Rifle, true, 2.7f);
            Assert.IsFalse(ReloadCancelRules.Evaluate(p, 0.72f, ReloadCancelReason.Fire).Allowed);
            Assert.IsFalse(ReloadCancelRules.Evaluate(p, 0.82f, ReloadCancelReason.Fire).Allowed);
            Assert.IsTrue(ReloadCancelRules.Evaluate(p, 0.93f, ReloadCancelReason.Fire).Allowed);
            Assert.IsTrue(ReloadCancelRules.Evaluate(p, 0.72f, ReloadCancelReason.WeaponSwap).Allowed);
        }

        [Test]
        public void Cancel_ShotgunNeedsLoadedShell_ToFire()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Shotgun, false, 3f);
            Assert.IsFalse(ReloadCancelRules.Evaluate(p, 0.4f, ReloadCancelReason.Fire, 0).Allowed);
            var d = ReloadCancelRules.Evaluate(p, 0.4f, ReloadCancelReason.Fire, 2);
            Assert.IsTrue(d.Allowed);
            Assert.IsTrue(d.AmmoCommitted);
        }

        [Test]
        public void CancelBlend_RewindsToTarget_AndWeightFades()
        {
            var p = ReloadPhasePlan.Build(ReloadKind.Rifle, false, 2.1f);
            var d = ReloadCancelRules.Evaluate(p, 0.5f, ReloadCancelReason.Manual);
            var b = ReloadCancelBlend.Start(0.5f, d);
            Assert.IsTrue(b.Active);
            float t, w;
            Assert.IsTrue(b.Step(0.01f, out t, out w));
            Assert.AreEqual(1f, w, 0.05f);
            Assert.IsTrue(t <= 0.5f);
            var guard = 0;
            while (b.Step(0.02f, out t, out w) && guard++ < 100) { }
            Assert.IsFalse(b.Active);
            Assert.IsTrue(guard < 100);
            Assert.AreEqual(0f, w, 0.0001f);
        }

        [Test]
        public void MagCheck_SkillControlsPrecision()
        {
            var vague = MagazineCheck.Estimate(14, 30, true, 0);
            Assert.AreEqual(MagEstimateKind.Vague, vague.Kind);
            Assert.AreEqual(-1, vague.Shown);
            Assert.AreEqual("Yarıya yakın", vague.Text);
            var approx = MagazineCheck.Estimate(14, 30, false, 12);
            Assert.AreEqual(MagEstimateKind.Approx, approx.Kind);
            Assert.AreEqual(15, approx.Shown);
            var exact = MagazineCheck.Estimate(14, 30, true, 25);
            Assert.AreEqual(MagEstimateKind.Exact, exact.Kind);
            Assert.AreEqual("14/30 +1", exact.Text);
        }

        [Test]
        public void MagCheck_VagueBuckets()
        {
            Assert.AreEqual("Boş", MagazineCheck.VagueText(0, 30));
            Assert.AreEqual("Neredeyse boş", MagazineCheck.VagueText(5, 30));
            Assert.AreEqual("Neredeyse dolu", MagazineCheck.VagueText(25, 30));
            Assert.AreEqual("Dolu", MagazineCheck.VagueText(30, 30));
        }

        [Test]
        public void MagCheck_DurationShrinksWithSkill_CappedAndInstantAtElite()
        {
            Assert.AreEqual(1.9f, MagazineCheck.DurationSeconds(0), 0.001f);
            Assert.Less(MagazineCheck.DurationSeconds(30), MagazineCheck.DurationSeconds(10));
            Assert.AreEqual(1.9f * 0.6f, MagazineCheck.DurationSeconds(50), 0.001f);
            Assert.Less(MagazineCheck.DurationSeconds(51), 0.5f);
        }

        [Test]
        public void MagCheckTimeline_PullPeaksBeforeReveal_AndReturnsHome()
        {
            var tl = MagCheckTimeline.Shared;
            Assert.AreEqual(1f, tl.MagPull.Evaluate(MagazineCheck.RevealFraction), 0.001f);
            Assert.AreEqual(0f, tl.MagPull.Evaluate(1f), 0.001f);
            Assert.AreEqual(0f, tl.Tilt.Evaluate(1f), 0.001f);
        }

        [Test]
        public void SwapTiming_PistolFasterThanMachinegun_FireReadyBeforeFullDraw()
        {
            Assert.Less(WeaponSwapTiming.DrawSeconds(ReloadKind.Pistol), WeaponSwapTiming.DrawSeconds(ReloadKind.Machinegun));
            Assert.Less(WeaponSwapTiming.FireReadySeconds(ReloadKind.Rifle), WeaponSwapTiming.DrawSeconds(ReloadKind.Rifle));
            Assert.Less(WeaponSwapTiming.HolsterSeconds(ReloadKind.Rifle), WeaponSwapTiming.DrawSeconds(ReloadKind.Rifle));
            Assert.AreEqual(1f, WeaponSwapTiming.RemainingFraction(0f, ReloadKind.Rifle), 0.0001f);
            Assert.AreEqual(0f, WeaponSwapTiming.RemainingFraction(5f, ReloadKind.Rifle), 0.0001f);
        }
    }
}
