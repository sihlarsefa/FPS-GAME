using System.Collections.Generic;
using NUnit.Framework;
using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class NetcodeV2Tests
    {
        private static PositionHistory.Sample S(Vector3 p) =>
            new PositionHistory.Sample(1, 0f, p, Quaternion.identity, 1.8f, 0.35f);

        // ---------------------------------------------------------------- hitscan
        [Test]
        public void Hitscan_ClampRewindTick()
        {
            Assert.AreEqual(100u, HitscanRules.ClampRewindTick(0, 100, 30, out var c)); Assert.IsFalse(c);
            Assert.AreEqual(100u, HitscanRules.ClampRewindTick(500, 100, 30, out c)); Assert.IsTrue(c);
            Assert.AreEqual(70u, HitscanRules.ClampRewindTick(5, 100, 30, out c)); Assert.IsTrue(c);
            Assert.AreEqual(90u, HitscanRules.ClampRewindTick(90, 100, 30, out c)); Assert.IsFalse(c);
            Assert.AreEqual(5u, HitscanRules.ClampRewindTick(5, 10, 30, out c)); Assert.IsFalse(c);
        }

        [Test]
        public void Hitscan_EvaluateRejectsRateAmmoAndGarbage()
        {
            var eye = new Vector3(0, 1.5f, 0);
            var dir = Vector3.forward;
            Assert.AreEqual(ShotReject.FireRate, HitscanRules.Evaluate(1.01f, 1.0f, 20f, 10, eye, dir, eye, 50, 50, 30).Reject);
            Assert.AreEqual(ShotReject.NoAmmo, HitscanRules.Evaluate(5f, 1.0f, 20f, 0, eye, dir, eye, 50, 50, 30).Reject);
            Assert.AreEqual(ShotReject.BadDirection, HitscanRules.Evaluate(5f, 1.0f, 20f, 5, eye, Vector3.zero, eye, 50, 50, 30).Reject);
            Assert.AreEqual(ShotReject.BadNumbers, HitscanRules.Evaluate(5f, 1.0f, 20f, 5, new Vector3(float.NaN, 0, 0), dir, eye, 50, 50, 30).Reject);
        }

        [Test]
        public void Hitscan_EvaluateClampsOriginAndTick()
        {
            var eye = new Vector3(0, 1.5f, 0);
            var ok = HitscanRules.Evaluate(5f, 0f, 20f, 5, eye + Vector3.right * 0.5f, new Vector3(0, 0, 2), eye, 80, 100, 30);
            Assert.IsTrue(ok.Accepted);
            Assert.AreEqual(eye.x + 0.5f, ok.Origin.x, 1e-5f);
            Assert.AreEqual(1f, ok.Direction.magnitude, 1e-5f);
            Assert.AreEqual(80u, ok.RewindTick);
            Assert.AreEqual(20u, ok.RewindTicks);

            var cheat = HitscanRules.Evaluate(5f, 0f, 20f, 5, eye + Vector3.right * 40f, Vector3.forward, eye, 1, 100, 30);
            Assert.AreEqual(eye, cheat.Origin);
            Assert.IsTrue(cheat.WasClamped);
            Assert.AreEqual(70u, cheat.RewindTick);
        }

        [Test]
        public void Hitscan_PickVictimNearestAndWallBlocks()
        {
            var list = new List<HitCandidate>
            {
                new HitCandidate(1, S(new Vector3(0, 0.9f, 20))),
                new HitCandidate(2, S(new Vector3(0, 0.9f, 10))),
                new HitCandidate(3, S(new Vector3(5, 0.9f, 10)))
            };
            var origin = new Vector3(0, 1.2f, 0);
            Assert.IsTrue(HitscanRules.PickVictim(list, origin, Vector3.forward, float.MaxValue, out var id, out var dist, out _));
            Assert.AreEqual(2, id);
            Assert.AreEqual(9.65f, dist, 0.05f);
            // Duvar 5 m'de: kimse vurulamaz.
            Assert.IsFalse(HitscanRules.PickVictim(list, origin, Vector3.forward, 5f, out id, out _, out _));
            Assert.AreEqual(-1, id);
        }

        [Test]
        public void Hitscan_RewoundPositionHitsWhereTargetWas()
        {
            var h = new PositionHistory(1f, 30f);
            for (uint t = 100; t < 130; t++)
                h.Record(t, t / 30f, new Vector3(t - 100, 0.9f, 10), Quaternion.identity, 1.8f, 0.35f);
            // Hedef şimdi x=29 civarında; atıcı 15 tick önceki görüntüyü (x=14) nişanladı.
            Assert.IsTrue(h.TrySampleTick(114, out var past));
            var cands = new List<HitCandidate> { new HitCandidate(7, past) };
            var origin = new Vector3(14, 1.0f, 0);
            Assert.IsTrue(HitscanRules.PickVictim(cands, origin, Vector3.forward, float.MaxValue, out var id, out _, out _));
            Assert.AreEqual(7, id);
            Assert.IsTrue(h.TrySampleTick(129, out var now));
            cands[0] = new HitCandidate(7, now);
            Assert.IsFalse(HitscanRules.PickVictim(cands, origin, Vector3.forward, float.MaxValue, out _, out _, out _));
        }

        [Test]
        public void Hitscan_HeadshotAndDamage()
        {
            Assert.IsTrue(CapsuleRay.Raycast(new Vector3(0, 0.9f, 10), Quaternion.identity, 1.8f, 0.35f,
                new Vector3(0, 1.7f, 0), Vector3.forward, 100f, out _, out var head));
            Assert.IsTrue(head);
            Assert.IsTrue(CapsuleRay.Raycast(new Vector3(0, 0.9f, 10), Quaternion.identity, 1.8f, 0.35f,
                new Vector3(0, 0.3f, 0), Vector3.forward, 100f, out _, out head));
            Assert.IsFalse(head);
            Assert.AreEqual(70f, HitscanRules.DamageFor(true));
            Assert.AreEqual(28f, HitscanRules.DamageFor(false));
        }

        // ---------------------------------------------------------------- throw
        [Test]
        public void Throw_UnknownCodeRejectedAndCooldowns()
        {
            var g = new ThrowGate();
            Assert.IsFalse(g.TryAccept(9, 0f));
            Assert.AreEqual(1, g.RejectedUnknown);
            Assert.IsTrue(g.TryAccept(1, 10f));
            Assert.IsFalse(g.TryAccept(1, 10.2f));
            Assert.IsFalse(g.TryAccept(3, 10.2f)); // genel bekleme
            Assert.IsTrue(g.TryAccept(3, 10.6f));
            Assert.IsFalse(g.TryAccept(3, 11.5f)); // molotof 1.5 sn
            Assert.IsTrue(g.TryAccept(3, 12.2f));
        }

        [Test]
        public void Throw_SanitizeClampsAndRejectsNaN()
        {
            var eye = new Vector3(0, 1.6f, 0);
            Assert.IsTrue(ThrowGate.Sanitize(eye + Vector3.up * 10f, new Vector3(100, 0, 0), eye, out var o, out var v));
            Assert.AreEqual(eye, o);
            Assert.AreEqual(ThrowGate.MaxSpeed, v.magnitude, 1e-3f);
            Assert.IsFalse(ThrowGate.Sanitize(eye, new Vector3(float.NaN, 0, 0), eye, out _, out _));
            Assert.IsFalse(ThrowGate.Sanitize(eye, new Vector3(float.PositiveInfinity, 0, 0), eye, out _, out _));
        }

        // ---------------------------------------------------------------- join state
        [Test]
        public void JoinState_RoundTrip()
        {
            var s = new JoinState { ZonePhaseIndex = 3, ZoneStage = 2, StageRemaining = 12.5f, StageDuration = 40f,
                CenterX = 10, CenterZ = -20, Radius = 300, Dps = 2.5f, NextCenterX = 5, NextCenterZ = 6, NextRadius = 150 };
            for (var i = 0; i < 40; i++) s.AliveIds.Add(i * 3 - 1);
            var bytes = s.Encode();
            Assert.AreEqual(JoinState.HeaderBytes + 40 * 4, bytes.Length);
            Assert.IsTrue(JoinState.TryDecode(bytes, out var d));
            Assert.AreEqual(3, d.ZonePhaseIndex);
            Assert.AreEqual(2, d.ZoneStage);
            Assert.AreEqual(12.5f, d.StageRemaining);
            Assert.AreEqual(150f, d.NextRadius);
            Assert.AreEqual(40, d.AliveCount);
            Assert.IsTrue(d.IsAlive(-1));
            Assert.IsTrue(d.IsAlive(116));
            Assert.IsFalse(d.IsAlive(3));
        }

        [Test]
        public void JoinState_RejectsGarbage()
        {
            Assert.IsFalse(JoinState.TryDecode(null, out _));
            Assert.IsFalse(JoinState.TryDecode(new byte[5], out _));
            var s = new JoinState { Radius = 10f };
            s.AliveIds.Add(1);
            var b = s.Encode();
            var wrongVersion = (byte[])b.Clone(); wrongVersion[0] = 99;
            Assert.IsFalse(JoinState.TryDecode(wrongVersion, out _));
            var truncated = new byte[b.Length - 1]; System.Array.Copy(b, truncated, truncated.Length);
            Assert.IsFalse(JoinState.TryDecode(truncated, out _));
            var huge = (byte[])b.Clone(); huge[JoinState.HeaderBytes - 2] = 0xFF; huge[JoinState.HeaderBytes - 1] = 0xFF;
            Assert.IsFalse(JoinState.TryDecode(huge, out _));
            var nan = new JoinState { Radius = float.NaN }.Encode();
            Assert.IsFalse(JoinState.TryDecode(nan, out _));
        }

        [Test]
        public void JoinState_CapsAliveAndCompensatesLatency()
        {
            var s = new JoinState { ZoneStage = 1, StageRemaining = 5f, StageDuration = 10f, Radius = 1f };
            for (var i = 0; i < 300; i++) s.AliveIds.Add(i);
            Assert.IsTrue(JoinState.TryDecode(s.Encode(), out var d));
            Assert.AreEqual(JoinState.MaxAlive, d.AliveCount);
            d.CompensateLatency(0.1f);
            Assert.AreEqual(4.9f, d.StageRemaining, 1e-4f);
            d.CompensateLatency(100f);
            Assert.AreEqual(0f, d.StageRemaining);
        }

        // ---------------------------------------------------------------- tick metrics
        [Test]
        public void TickMetrics_BudgetOverrunsAndPercentile()
        {
            var m = new ServerTickMetrics(30f, 100);
            for (var i = 0; i < 95; i++) m.Record(0.005f);
            for (var i = 0; i < 5; i++) m.Record(0.05f);
            Assert.AreEqual(100, m.Count);
            Assert.AreEqual(1f / 30f, m.BudgetSeconds, 1e-6f);
            Assert.AreEqual(0.05f, m.OverrunRatio, 1e-4f);
            Assert.AreEqual(5, m.TotalOverruns);
            Assert.AreEqual(0.005f, m.Percentile(0.5f), 1e-6f);
            Assert.AreEqual(0.05f, m.Percentile(1f), 1e-6f);
            Assert.AreEqual(0.05f, m.WorstSeconds, 1e-6f);
            Assert.IsFalse(m.IsHealthy); // %5 aşım sınırda: < 0.05 değil
            m.Record(float.NaN); m.Record(-1f);
            Assert.AreEqual(100, m.TotalSamples);
            StringAssert.Contains("ms", m.Summary());
        }

        [Test]
        public void TickMetrics_HealthyWhenLight()
        {
            var m = new ServerTickMetrics(30f, 30);
            for (var i = 0; i < 30; i++) m.Record(0.004f);
            Assert.IsTrue(m.IsHealthy);
            Assert.Less(m.BudgetUsagePercent, 15f);
        }

        // ---------------------------------------------------------------- remote interpolation
        [Test]
        public void RemoteInterpolation_RendersBehindAndSmooths()
        {
            var r = new RemoteInterpolation(30f);
            for (var i = 0; i < 30; i++)
                r.Feed(i / 30f, i / 30f, new Vector3(i * 0.2f, 0, 0), 0f);
            Assert.AreEqual(30, r.Count);
            Assert.IsTrue(r.Evaluate(29f / 30f, 0.016f, out var p));
            // Render zamanı ≈ now - 2 tick: konum son snapshot'ın gerisinde, ortasında.
            Assert.Less(p.Position.x, 29 * 0.2f);
            Assert.Greater(p.Position.x, 25 * 0.2f);
        }

        [Test]
        public void RemoteInterpolation_TeleportClearsBufferAndStaleIgnored()
        {
            var r = new RemoteInterpolation(30f);
            Assert.IsTrue(r.Feed(0f, 0f, Vector3.zero, 0f));
            Assert.IsTrue(r.Feed(0.033f, 0.033f, Vector3.right, 0f));
            Assert.IsFalse(r.Feed(0.04f, 0.033f, Vector3.right, 0f));
            Assert.IsTrue(r.Feed(0.066f, 0.066f, new Vector3(500, 0, 0), 0f));
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(1, r.Teleports);
            Assert.IsFalse(new RemoteInterpolation().Evaluate(1f, 0.016f, out _));
        }

        // ---------------------------------------------------------------- reconcile
        [Test]
        public void Reconcile_ClassifyAndSmoothFactor()
        {
            Assert.AreEqual(ReconcilePolicy.Action.None, ReconcilePolicy.Classify(0.1f, 0.25f, 3f));
            Assert.AreEqual(ReconcilePolicy.Action.Smooth, ReconcilePolicy.Classify(1f, 0.25f, 3f));
            Assert.AreEqual(ReconcilePolicy.Action.Snap, ReconcilePolicy.Classify(3f, 0.25f, 3f));
            var a = ReconcilePolicy.SmoothFactor(0.016f);
            var b = ReconcilePolicy.SmoothFactor(0.033f);
            Assert.Greater(b, a);
            Assert.Less(b, 1f);
            Assert.AreEqual(0f, ReconcilePolicy.SmoothFactor(0f));
            // Kare bağımsızlık: 2 yarım adım = 1 tam adım.
            Assert.AreEqual(b, 1f - (1f - a) * (1f - ReconcilePolicy.SmoothFactor(0.017f)), 1e-3f);
        }

        [Test]
        public void Reconcile_PendingCommandsReplayAfterMismatch()
        {
            var cmd = new Project.Core.Domain.PlayerCommand(0, 1f, 0f, 0f, 0f, Project.Core.Domain.PlayerButtons.None, 0, 0);
            var b = new PredictionBuffer(16);
            for (uint i = 1; i <= 6; i++)
            {
                b.Record(i, cmd);
                b.SetPrediction(i, new Vector3(0, 0, i * 0.18f));
            }
            // Sunucu 3. komutta 1 m geride: hata var, 4..6 yeniden oynatılmak üzere kalır.
            Assert.IsTrue(b.Acknowledge(3, new Vector3(0, 0, 0.54f - 1f), 0.25f, out var err));
            Assert.AreEqual(1f, err, 1e-4f);
            Assert.AreEqual(3, b.Count);
            Assert.AreEqual(4u, b.At(0).Sequence);
            Assert.AreEqual(6u, b.At(2).Sequence);
        }
    }
}
