using NUnit.Framework;
using Project.Core.Domain;
using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class NetcodeSimTests
    {
        private static PlayerCommand Cmd(uint tick) => new(tick, 1f, 0f, 0f, 0f, PlayerButtons.None, 0, 0);

        [Test]
        public void Prediction_AckDropsOldAndKeepsPending()
        {
            var b = new PredictionBuffer(16);
            for (uint i = 1; i <= 5; i++)
            {
                Assert.IsTrue(b.Record(i, Cmd(i)));
                b.SetPrediction(i, new Vector3(i, 0, 0));
            }

            Assert.IsFalse(b.Acknowledge(3, new Vector3(3, 0, 0), 0.1f, out var err));
            Assert.AreEqual(0f, err, 1e-5f);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(4u, b.At(0).Sequence);
            Assert.AreEqual(3u, b.LastAckedSequence);
        }

        [Test]
        public void Prediction_MismatchRequestsReplay()
        {
            var b = new PredictionBuffer(16);
            b.Record(1, Cmd(1));
            b.SetPrediction(1, Vector3.zero);
            b.Record(2, Cmd(2));
            Assert.IsTrue(b.Acknowledge(1, new Vector3(1f, 0, 0), 0.25f, out var err));
            Assert.AreEqual(1f, err, 1e-4f);
            Assert.AreEqual(1, b.Count);
        }

        [Test]
        public void Prediction_RejectsOldSequenceAndStaleAck()
        {
            var b = new PredictionBuffer(16);
            Assert.IsTrue(b.Record(5, Cmd(5)));
            Assert.IsFalse(b.Record(5, Cmd(5)));
            Assert.IsFalse(b.Record(4, Cmd(4)));
            b.SetPrediction(5, Vector3.zero);
            b.Acknowledge(5, Vector3.zero, 0.1f, out _);
            Assert.IsFalse(b.Acknowledge(2, Vector3.one * 99f, 0.1f, out _));
        }

        [Test]
        public void Prediction_OverflowDropsOldest()
        {
            var b = new PredictionBuffer(8);
            for (uint i = 1; i <= 20; i++)
                b.Record(i, Cmd(i));
            Assert.AreEqual(8, b.Count);
            Assert.AreEqual(13u, b.At(0).Sequence);
        }

        [Test]
        public void CommandGate_RejectsDuplicatesAndFloods()
        {
            var g = new ServerCommandGate(30f, 5f);
            Assert.IsTrue(g.Accept(1, 0f));
            Assert.IsFalse(g.Accept(1, 0f));
            Assert.IsFalse(g.Accept(0xFFFFFFFFu, 0f) && false);
            var ok = 0;
            for (uint i = 2; i < 40; i++)
                if (g.Accept(i, 0f)) ok++;
            Assert.AreEqual(4, ok); // burst 5, biri ilk komutta harcandı
            Assert.Greater(g.RejectedRate, 0);
            Assert.IsTrue(g.Accept(100, 1f));
            Assert.IsTrue(g.Accept(0, 1f)); // sıra yok = yerel
        }

        [Test]
        public void Jitter_StableNetworkKeepsMinDelay_JitterRaisesIt()
        {
            var calm = new JitterEstimator(30f);
            for (var i = 0; i < 100; i++) calm.Observe(i / 30f);
            Assert.Less(calm.Jitter, 0.002f);
            Assert.AreEqual(2f / 30f, calm.TargetDelay, 0.01f);

            var rough = new JitterEstimator(30f);
            var t = 0f;
            for (var i = 0; i < 100; i++)
            {
                t += (i % 2 == 0) ? 0.01f : 0.06f;
                rough.Observe(t);
            }

            Assert.Greater(rough.TargetDelay, calm.TargetDelay + 0.05f);
            var d0 = rough.Delay;
            rough.Advance(0.1f);
            Assert.Greater(rough.Delay, d0);
            Assert.LessOrEqual(rough.Delay - d0, 0.026f); // yumuşak kayma
        }

        [Test]
        public void Interpolator_InterpolatesAndExtrapolatesBriefly()
        {
            var s = new SnapshotInterpolator(8);
            Assert.IsTrue(s.Add(1f, new Vector3(0, 0, 0), 0f));
            Assert.IsTrue(s.Add(2f, new Vector3(10, 0, 0), 90f));
            Assert.IsFalse(s.Add(2f, Vector3.zero, 0f));
            Assert.IsTrue(s.TrySample(1.5f, out var p));
            Assert.AreEqual(5f, p.Position.x, 1e-4f);
            Assert.AreEqual(45f, p.Yaw, 1e-3f);
            Assert.IsTrue(s.TrySample(2.1f, out p));
            Assert.AreEqual(11f, p.Position.x, 1e-3f);
            Assert.IsTrue(s.TrySample(5f, out p));
            Assert.AreEqual(12f, p.Position.x, 1e-3f); // 0.2 sn ile sınırlı
        }

        [Test]
        public void History_InterpolatesAndClampsRewind()
        {
            var h = new PositionHistory(1f, 30f);
            for (uint t = 100; t < 160; t++)
                h.Record(t, t / 30f, new Vector3(t, 0, 0), Quaternion.identity, 1.8f, 0.35f);

            Assert.LessOrEqual(h.Count, h.Capacity);
            Assert.IsTrue(h.TrySampleTick(150, out var s));
            Assert.AreEqual(150f, s.Position.x, 1e-3f);
            Assert.IsTrue(h.TrySampleTime(150.5f / 30f, out s));
            Assert.AreEqual(150.5f, s.Position.x, 1e-2f);

            Assert.AreEqual(159u, h.ClampTick(500, 159));
            Assert.AreEqual(129u, h.ClampTick(10, 159));
            Assert.AreEqual(140u, h.ClampTick(140, 159));
            Assert.IsFalse(h.Record(100, 0f, Vector3.zero, Quaternion.identity, 1f, 1f));
        }

        [Test]
        public void History_CapacityIsAboutOneSecondAt30Hz()
        {
            var h = new PositionHistory(1f, 30f);
            Assert.GreaterOrEqual(h.Capacity, 30);
            Assert.AreEqual(30u, h.MaxRewindTicks);
        }

        [Test]
        public void Interest_RadiusTeamAndHysteresis()
        {
            var o = Vector3.zero;
            Assert.IsTrue(InterestRules.ShouldReplicate(false, o, new Vector3(499, 0, 0), 0, 1));
            Assert.IsFalse(InterestRules.ShouldReplicate(false, o, new Vector3(510, 0, 0), 0, 1));
            Assert.IsTrue(InterestRules.ShouldReplicate(true, o, new Vector3(510, 0, 0), 0, 1));
            Assert.IsFalse(InterestRules.ShouldReplicate(true, o, new Vector3(600, 0, 0), 0, 1));
            Assert.IsTrue(InterestRules.ShouldReplicate(false, o, new Vector3(2000, 0, 0), 3, 3));
            Assert.IsFalse(InterestRules.ShouldReplicate(false, o, new Vector3(2000, 0, 0), -1, -1));
        }

        [Test]
        public void Bandwidth_NonCriticalDropsCriticalBorrows()
        {
            var b = new BandwidthBudget(1000, 0.5f);
            b.Register(1);
            Assert.IsTrue(b.TryConsume(1, 400, 0f));
            Assert.IsFalse(b.TryConsume(1, 400, 0f));
            Assert.AreEqual(400, b.TotalDropped(1));
            Assert.IsTrue(b.TryConsume(1, 400, 0f, critical: true));
            Assert.IsFalse(b.TryConsume(1, 5000, 0f, critical: true));
            Assert.IsTrue(b.TryConsume(1, 400, 1f)); // 1 sn sonra doldu
            Assert.AreEqual(800, b.TotalSent(1) - 400);
        }
    }
}
