using NUnit.Framework;
using Project.Infrastructure.Rendering.Perf;

namespace Project.Tests.EditMode
{
    public sealed class PerfToolsTests
    {
        private sealed class Counter : IBatchTickable
        {
            public int Ticks; public float LastDt; public bool Throw;
            public void BatchTick(float dt) { Ticks++; LastDt = dt; if (Throw) throw new System.InvalidOperationException("x"); }
        }

        private static FrameTimeTracker Filled(int n, float ms, float spikeMs = 0f, int spikeEvery = 0)
        {
            var t = new FrameTimeTracker(240);
            for (var i = 0; i < n; i++)
                t.Push((spikeEvery > 0 && i % spikeEvery == 0 ? spikeMs : ms) / 1000f);
            return t;
        }

        [Test]
        public void FrameTime_SteadyHasNoJitterOrHitches()
        {
            var s = Filled(200, 16.67f).Compute();
            Assert.AreEqual(16.67f, s.AvgMs, 0.05f);
            Assert.AreEqual(16.67f, s.P99Ms, 0.05f);
            Assert.AreEqual(0f, s.JitterMs, 0.01f);
            Assert.AreEqual(0, s.HitchCount);
            Assert.AreEqual(60f, s.OnePercentLowFps, 0.5f);
        }

        [Test]
        public void FrameTime_SpikesRaiseTailAndHitches()
        {
            var s = Filled(200, 10f, 50f, 20).Compute();
            Assert.AreEqual(10, s.HitchCount);
            Assert.Greater(s.P99Ms, 40f);
            Assert.Less(s.MedianMs, 11f);
            Assert.Greater(s.JitterMs, 3f);
            Assert.Less(s.OnePercentLowFps, 30f);
        }

        [Test]
        public void FrameTime_IgnoresInvalidAndWraps()
        {
            var t = new FrameTimeTracker(8);
            t.Push(-1f); t.Push(float.NaN); t.Push(0f);
            Assert.AreEqual(0, t.Count);
            for (var i = 0; i < 20; i++) t.Push(0.01f);
            t.Push(5f); // 1 sn'ye kırpılır
            var s = t.Compute();
            Assert.AreEqual(8, s.Samples);
            Assert.AreEqual(1000f, s.MaxMs, 0.1f);
            Assert.AreEqual(0, new FrameTimeTracker().Compute().Samples);
        }

        [Test]
        public void Alloc_TotalsDeltaAndCollections()
        {
            var a = new AllocationTracker(10);
            a.PushTotals(1000, 0, 0.016f);          // ilk örnek: referans
            a.PushTotals(1600, 0, 0.016f);          // +600
            a.PushTotals(1200, 1, 0.016f);          // düştü: GC
            a.PushTotals(1500, 1, 0.016f);          // +300
            var s = a.Compute(256);
            Assert.AreEqual(4, s.Frames);
            Assert.AreEqual(600L, s.PeakBytesInFrame);
            Assert.AreEqual(1, s.Collections);
            Assert.AreEqual(2, s.AllocatingFrames);
            Assert.AreEqual(225f, s.AvgBytesPerFrame, 0.01f);
        }

        [Test]
        public void Alloc_IsHotNeedsWindowAndThreshold()
        {
            var a = new AllocationTracker(100);
            for (var i = 0; i < 59; i++) a.PushFrame(5000, 0, 0.016f);
            Assert.IsFalse(AllocationTracker.IsHot(a.Compute()));
            a.PushFrame(5000, 0, 0.016f);
            Assert.IsTrue(AllocationTracker.IsHot(a.Compute()));
            var quiet = new AllocationTracker(100);
            for (var i = 0; i < 80; i++) quiet.PushFrame(0, 0, 0.016f);
            Assert.IsFalse(AllocationTracker.IsHot(quiet.Compute()));
        }

        [Test]
        public void Pacing_ReducesAfterConsecutiveBadThenRestoresSlowly()
        {
            var adv = new FramePacingAdvisor();
            var bad = Filled(200, 30f).Compute();
            Assert.AreEqual(PacingAdvice.Hold, adv.Evaluate(bad, 60));
            Assert.AreEqual(PacingAdvice.Hold, adv.Evaluate(bad, 60));
            Assert.AreEqual(PacingAdvice.ReduceLoad, adv.Evaluate(bad, 60));
            Assert.AreEqual(0.85f, adv.BudgetScale, 0.001f);

            var good = Filled(200, 14f).Compute();
            var restored = false;
            for (var i = 0; i < 30 && !restored; i++)
                restored = adv.Evaluate(good, 60) == PacingAdvice.RestoreLoad;
            Assert.IsTrue(restored);
            Assert.Greater(adv.BudgetScale, 0.85f);
        }

        [Test]
        public void Pacing_NeedsEnoughSamplesAndClampsBudget()
        {
            var adv = new FramePacingAdvisor();
            Assert.AreEqual(PacingAdvice.Hold, adv.Evaluate(Filled(20, 50f).Compute(), 60));
            var bad = Filled(200, 50f).Compute();
            for (var i = 0; i < 100; i++) adv.Evaluate(bad, 60);
            Assert.AreEqual(FramePacingAdvisor.MinBudget, adv.BudgetScale, 0.001f);
            Assert.AreEqual(16.667f, FramePacingAdvisor.TargetMs(60), 0.01f);
        }

        [Test]
        public void Tick_RunsAtIntervalWithDt()
        {
            var s = new TickScheduler();
            var c = new Counter();
            Assert.IsTrue(s.Register(c, 1f, 0.0));
            Assert.IsFalse(s.Register(c, 1f, 0.0));
            var ran = 0;
            for (var t = 0.0; t <= 3.0001; t += 0.1) ran += s.Run(t);
            Assert.GreaterOrEqual(c.Ticks, 3);
            Assert.Less(c.Ticks, 5);
            Assert.AreEqual(ran, c.Ticks);
            Assert.Greater(c.LastDt, 0.9f);
        }

        [Test]
        public void Tick_UnregisterAndEveryFrame()
        {
            var s = new TickScheduler();
            var a = new Counter(); var b = new Counter();
            s.Register(a, 0f, 0.0); s.Register(b, 0f, 0.0);
            s.Run(0.1);
            Assert.AreEqual(1, a.Ticks);
            Assert.IsTrue(s.Unregister(a));
            Assert.IsFalse(s.Unregister(a));
            s.Run(0.2);
            Assert.AreEqual(1, a.Ticks);
            Assert.AreEqual(2, b.Ticks);
            Assert.AreEqual(1, s.Count);
        }

        [Test]
        public void Tick_ExceptionIsReportedAndOthersStillRun()
        {
            var errors = 0;
            var s = new TickScheduler(_ => errors++);
            var bad = new Counter { Throw = true };
            var ok = new Counter();
            s.Register(bad, 0f, 0.0); s.Register(ok, 0f, 0.0);
            s.Run(1.0);
            Assert.AreEqual(1, errors);
            Assert.AreEqual(1, ok.Ticks);
        }

        [Test]
        public void Tick_StaggersPhasesAcrossEntries()
        {
            var s = new TickScheduler();
            var list = new Counter[8];
            for (var i = 0; i < list.Length; i++) { list[i] = new Counter(); s.Register(list[i], 1f, 0.0); }
            // Aralığın ilk yarısında hepsi birden çalışmamalı.
            var firstFrame = s.Run(0.0);
            Assert.Less(firstFrame, list.Length);
        }

        [Test]
        public void GcTuning_SliceScalesWithFpsAndClamps()
        {
            var normal = GcTuningMath.SliceNanoseconds(60, false);
            var heavy = GcTuningMath.SliceNanoseconds(60, true);
            Assert.Greater(heavy, normal);
            Assert.GreaterOrEqual(GcTuningMath.SliceNanoseconds(240, false), GcTuningMath.MinSliceNs);
            Assert.LessOrEqual(GcTuningMath.SliceNanoseconds(15, true), GcTuningMath.MaxSliceNs);
        }

        [Test]
        public void GcTuning_SafePointRules()
        {
            const long mb = 1024 * 1024;
            Assert.IsTrue(GcTuningMath.ShouldCollectAtSafePoint(true, 45f, 20 * mb));
            Assert.IsFalse(GcTuningMath.ShouldCollectAtSafePoint(false, 45f, 20 * mb));
            Assert.IsFalse(GcTuningMath.ShouldCollectAtSafePoint(true, 5f, 20 * mb));
            Assert.IsFalse(GcTuningMath.ShouldCollectAtSafePoint(true, 45f, 2 * mb));
        }
    }
}
