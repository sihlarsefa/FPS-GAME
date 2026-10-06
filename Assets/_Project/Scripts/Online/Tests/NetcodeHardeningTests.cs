using NUnit.Framework;
using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class NetcodeHardeningTests
    {
        private static PlayerSnap Snap(float x = 10f, float yaw = 90f, float hp = 1f) =>
            PlayerSnap.From(7, new Vector3(x, 1f, 5f), yaw, 0f, hp, 0);

        [Test]
        public void Delta_UnchangedIsFiveBytes()
        {
            var a = Snap();
            Assert.AreEqual(5, SnapshotDelta.EncodedSize(a, a));
            var buf = new byte[32]; var o = 0;
            SnapshotDelta.WriteDelta(buf, ref o, a, a);
            Assert.AreEqual(5, o);
        }

        [Test]
        public void Delta_RoundTripSmallAndWide()
        {
            var a = Snap(10f, 90f, 1f);
            foreach (var b in new[] { Snap(10.5f, 91f, 0.5f), Snap(300f, 10f, 0f) })
            {
                var buf = new byte[64]; var o = 0;
                SnapshotDelta.WriteDelta(buf, ref o, a, b);
                Assert.AreEqual(SnapshotDelta.EncodedSize(a, b), o);
                var rd = 0;
                Assert.IsTrue(SnapshotDelta.TryRead(buf, ref rd, true, a, out var r));
                Assert.IsTrue(r.Equals(b));
                Assert.AreEqual(o, rd);
            }
        }

        [Test]
        public void Delta_FullRoundTripAndMuchSmallerDelta()
        {
            var a = Snap(10f); var b = Snap(10.3f);
            var buf = new byte[64]; var o = 0;
            SnapshotDelta.WriteFull(buf, ref o, b);
            var rd = 0;
            Assert.IsTrue(SnapshotDelta.TryRead(buf, ref rd, false, default, out var r));
            Assert.IsTrue(r.Equals(b));
            Assert.Greater(o, SnapshotDelta.EncodedSize(a, b));
        }

        [Test]
        public void Delta_RejectsGarbage()
        {
            var a = Snap();
            var rd = 0;
            Assert.IsFalse(SnapshotDelta.TryRead(new byte[3], ref rd, true, a, out var r));
            var buf = new byte[32]; var o = 0;
            SnapshotDelta.WriteDelta(buf, ref o, a, Snap(500f));
            rd = 0;
            Assert.IsFalse(SnapshotDelta.TryRead(buf, ref rd, false, default, out r)); // baseline yok + delta
            var trunc = new byte[o - 2]; System.Array.Copy(buf, trunc, trunc.Length);
            rd = 0;
            Assert.IsFalse(SnapshotDelta.TryRead(trunc, ref rd, true, a, out r));
        }

        [Test]
        public void Tiers_ClassifyByDistanceTeamAndSight()
        {
            var o = Vector3.zero;
            Assert.AreEqual(InterestTier.Near, InterestTiers.Classify(false, o, new Vector3(30, 0, 0), 0, 1, true));
            Assert.AreEqual(InterestTier.Mid, InterestTiers.Classify(false, o, new Vector3(150, 0, 0), 0, 1, true));
            Assert.AreEqual(InterestTier.Far, InterestTiers.Classify(false, o, new Vector3(400, 0, 0), 0, 1, true));
            Assert.AreEqual(InterestTier.Culled, InterestTiers.Classify(false, o, new Vector3(700, 0, 0), 0, 1, true));
            Assert.AreEqual(InterestTier.Near, InterestTiers.Classify(false, o, new Vector3(700, 0, 0), 2, 2, false));
            Assert.AreEqual(InterestTier.Mid, InterestTiers.Classify(false, o, new Vector3(30, 0, 0), 0, 1, false));
        }

        [Test]
        public void Tiers_FarIsFiveHz()
        {
            Assert.AreEqual(12, InterestTiers.TickInterval(InterestTier.Far, 60));
            Assert.AreEqual(3, InterestTiers.TickInterval(InterestTier.Mid, 60));
            Assert.AreEqual(1, InterestTiers.TickInterval(InterestTier.Near, 60));
            Assert.AreEqual(0, InterestTiers.TickInterval(InterestTier.Culled, 60));
            var sent = 0;
            for (uint t = 0; t < 60; t++) if (InterestTiers.ShouldSend(InterestTier.Far, t, 3, 60)) sent++;
            Assert.AreEqual(5, sent);
        }

        [Test]
        public void Reconnect_BackoffAndResync()
        {
            Assert.AreEqual(0.5f, ReconnectFlow.BackoffDelay(1), 1e-5f);
            Assert.AreEqual(2f, ReconnectFlow.BackoffDelay(3), 1e-5f);
            Assert.AreEqual(8f, ReconnectFlow.BackoffDelay(20), 1e-5f);

            var f = new ReconnectFlow(3);
            f.OnConnectionLost(0f);
            Assert.IsFalse(f.TryBeginAttempt(0.1f));
            Assert.IsTrue(f.TryBeginAttempt(0.6f));
            f.OnTransportRestored();
            Assert.AreEqual(ReconnectState.Resyncing, f.State);
            Assert.IsFalse(f.AcceptDeltas);
            f.OnFullSnapshotReceived();
            Assert.AreEqual(ReconnectState.Resyncing, f.State);
            f.OnJoinStateReceived();
            Assert.AreEqual(ReconnectState.Connected, f.State);
            Assert.IsTrue(f.AcceptDeltas);
        }

        [Test]
        public void Reconnect_FailsAfterMaxAttempts()
        {
            var f = new ReconnectFlow(2);
            f.OnConnectionLost(0f);
            var t = 0f;
            for (var i = 0; i < 2; i++)
            {
                t += 20f;
                Assert.IsTrue(f.TryBeginAttempt(t));
                f.OnAttemptFailed(t);
            }
            Assert.AreEqual(ReconnectState.Failed, f.State);
        }

        [Test]
        public void GraceTable_ClaimOnceAndExpire()
        {
            var g = new ReconnectGraceTable(10f);
            g.Hold("tok", 4, 0f);
            g.Hold("old", 9, 0f);
            Assert.IsTrue(g.TryClaim("tok", 5f, out var id)); Assert.AreEqual(4, id);
            Assert.IsFalse(g.TryClaim("tok", 6f, out id));
            var freed = g.Expire(11f);
            Assert.AreEqual(1, freed.Count); Assert.AreEqual(9, freed[0]);
            Assert.AreEqual(0, g.HeldCount);
            g.Hold("late", 3, 0f);
            Assert.IsFalse(g.TryClaim("late", 11f, out id));
        }
    }
}
