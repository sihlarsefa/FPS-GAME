using NUnit.Framework;
using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class NetcodeWiringTests
    {
        private static PlayerSnap Snap(int x, byte hp) => PlayerSnap.From(7, new Vector3(x / 100f, 0f, 0f), 10f, 0f, hp / 255f, 0);

        [Test]
        public void Stream_FirstFullThenDeltaThenKeyframe()
        {
            var ch = new SnapshotChannel();
            var rx = new SnapshotReceiver();
            var buf = new byte[64];
            var o = 0;
            Assert.IsTrue(ch.Write(buf, ref o, Snap(100, 255), 0));
            Assert.IsTrue(rx.Apply(buf, 0, false, out var full));
            Assert.IsTrue(full);
            o = 0;
            Assert.IsFalse(ch.Write(buf, ref o, Snap(110, 255), 1));
            Assert.Less(o, 10);
            Assert.IsTrue(rx.Apply(buf, 0, true, out full));
            Assert.IsFalse(full);
            Assert.AreEqual(110, rx.Last.X);
            o = 0;
            Assert.IsTrue(ch.Write(buf, ref o, Snap(120, 255), 40));
        }

        [Test]
        public void Receiver_RejectsDeltaWhileResyncingOrWithoutBaseline()
        {
            var ch = new SnapshotChannel();
            var buf = new byte[64];
            var o = 0;
            ch.Write(buf, ref o, Snap(0, 255), 0);
            o = 0;
            ch.Write(buf, ref o, Snap(5, 255), 1);
            var rx = new SnapshotReceiver();
            Assert.IsFalse(rx.Apply(buf, 0, true, out _));   // baseline yok
            Assert.IsFalse(rx.Apply(buf, 0, false, out _));  // eşitleme: delta yok
            Assert.IsFalse(rx.Apply(new byte[2], 0, true, out _));
        }

        [Test]
        public void Interest_FarSendsAt5HzAndCulledNever()
        {
            var tier = InterestTiers.Classify(false, Vector3.zero, new Vector3(400f, 0f, 0f), 0, 1, true);
            Assert.AreEqual(InterestTier.Far, tier);
            var sent = 0;
            for (uint t = 0; t < 30; t++)
                if (InterestTiers.ShouldSend(tier, t, 3, 30)) sent++;
            Assert.AreEqual(5, sent);
            Assert.IsFalse(InterestTiers.ShouldSend(InterestTier.Culled, 0, 0, 30));
        }

        [Test]
        public void Grace_ReclaimWithinWindowOnly()
        {
            var g = new ReconnectGraceTable(30f);
            g.Hold("tok", 5, 0f);
            Assert.IsTrue(g.TryClaim("tok", 10f, out var id));
            Assert.AreEqual(5, id);
            g.Hold("tok", 5, 0f);
            Assert.IsFalse(g.TryClaim("tok", 31f, out _));
        }

        [Test]
        public void ReconnectFlow_ResyncNeedsBothThenAcceptsDeltas()
        {
            var f = new ReconnectFlow();
            f.OnConnectionLost(0f);
            Assert.IsTrue(f.TryBeginAttempt(1f));
            f.OnTransportRestored();
            Assert.IsFalse(f.AcceptDeltas);
            f.OnFullSnapshotReceived();
            Assert.IsFalse(f.AcceptDeltas);
            f.OnJoinStateReceived();
            Assert.IsTrue(f.AcceptDeltas);
        }
    }
}
