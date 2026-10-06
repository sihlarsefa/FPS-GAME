using System;
using NUnit.Framework;
using Project.Online.Backend;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class ConnectionIdentityTests
    {
        [Test]
        public void Payload_RoundTrip()
        {
            var t = Guid.NewGuid();
            var p = ConnectionIdentity.Parse(ConnectionIdentity.Build(7, "aa.bb-cc_dd", t));
            Assert.IsTrue(p.Valid);
            Assert.AreEqual(7, p.PlayerId);
            Assert.AreEqual("aa.bb-cc_dd", p.Jwt);
            Assert.AreEqual(t, p.TicketId);
        }

        [Test]
        public void LegacyAndGarbagePayloads()
        {
            var legacy = ConnectionIdentity.Parse("harekat|3");
            Assert.IsTrue(legacy.Valid);
            Assert.IsFalse(legacy.HasToken);
            Assert.IsFalse(ConnectionIdentity.Parse("evil|3").Valid);
            Assert.IsFalse(ConnectionIdentity.Parse("harekat|x").Valid);
            Assert.IsFalse(ConnectionIdentity.Parse((byte[])null).Valid);
            Assert.IsFalse(ConnectionIdentity.Parse(new byte[ConnectionIdentity.MaxPayloadBytes + 1]).Valid);
        }

        [Test]
        public void Registry_BindsAndRejectsDuplicates()
        {
            var r = new IdentityRegistry();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var sq = Guid.NewGuid();
            Assert.IsTrue(r.TryBind(1, a, sq, out _));
            Assert.IsTrue(r.TryBind(1, a, sq, out _));
            Assert.IsFalse(r.TryBind(2, a, null, out _));
            Assert.IsFalse(r.TryBind(1, b, null, out _));
            Assert.IsFalse(r.TryBind(3, Guid.Empty, null, out _));
            Assert.IsTrue(r.IsHuman(1));
            Assert.IsFalse(r.IsHuman(9));
            Assert.IsTrue(r.TryGetSquad(1, out var s) && s == sq.ToString("D"));
            r.Release(1);
            Assert.IsTrue(r.TryBind(5, a, null, out _));
        }
    }
}
