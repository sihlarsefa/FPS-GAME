using System.Text;
using NUnit.Framework;
using Project.Online.Lan;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class LanProtocolTests
    {
        [Test]
        public void Beacon_RoundTrips()
        {
            var b = new LanBeacon("Kışla", 7777, "ayaz", 3, 10);
            var data = LanProtocol.Encode(b);
            Assert.IsTrue(LanProtocol.TryDecode(data, data.Length, out var d));
            Assert.AreEqual(b, d);
        }

        [Test]
        public void Beacon_NameStripsSeparatorAndClamps()
        {
            var b = new LanBeacon("a|b" + new string('x', 60), 7777, "kuzgun", 0, 4);
            Assert.IsFalse(b.Name.Contains("|"));
            Assert.LessOrEqual(b.Name.Length, LanProtocol.MaxNameLength);
        }

        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("HAREKAT|1|ad|0|kuzgun|1|4")]
        [TestCase("HAREKAT|2|ad|7777|kuzgun|1|4")]
        [TestCase("HAREKAT|1|ad|7777|kuzgun|x|4")]
        [TestCase("HAREKAT|1|ad|7777|kuzgun|1|0")]
        [TestCase("HAREKAT|1|ad|7777|kuzgun|1")]
        [TestCase("OTHER|1|ad|7777|kuzgun|1|4")]
        public void Decode_RejectsBad(string s)
        {
            var d = Encoding.UTF8.GetBytes(s);
            Assert.IsFalse(LanProtocol.TryDecode(d, d.Length, out _));
        }

        [Test]
        public void Decode_RejectsOversizedAndNull()
        {
            Assert.IsFalse(LanProtocol.TryDecode(null, 5, out _));
            Assert.IsFalse(LanProtocol.TryDecode(new byte[LanProtocol.MaxPacketBytes + 1], LanProtocol.MaxPacketBytes + 1, out _));
        }

        [Test]
        public void List_ExpiresAfterTtl()
        {
            var list = new LanServerList(3.0);
            var b = new LanBeacon("A", 7777, "kuzgun", 1, 4);
            Assert.IsTrue(list.Report("10.0.0.2", b, 0.0));
            Assert.IsFalse(list.Report("10.0.0.2", b, 1.0));      // aynı içerik: değişmedi
            Assert.IsFalse(list.Prune(3.5));                        // son görülme 1.0, 2.5 sn
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list.Prune(4.2));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void List_ContentChangeReportsAndSortsByName()
        {
            var list = new LanServerList();
            list.Report("10.0.0.3", new LanBeacon("Zeta", 7777, "kuzgun", 1, 4), 0);
            list.Report("10.0.0.2", new LanBeacon("Alfa", 7777, "kuzgun", 1, 4), 0);
            Assert.IsTrue(list.Report("10.0.0.2", new LanBeacon("Alfa", 7777, "kuzgun", 2, 4), 0.1));
            var s = list.Snapshot();
            Assert.AreEqual("Alfa", s[0].Beacon.Name);
            Assert.AreEqual("10.0.0.2:7777", s[0].Endpoint);
            Assert.AreEqual(2, s.Count);
        }

        [Test]
        public void List_CapsEntries()
        {
            var list = new LanServerList();
            for (var i = 0; i < LanServerList.MaxEntries + 5; i++)
                list.Report("10.0.1." + i, new LanBeacon("S", 7777, "kuzgun", 0, 4), 0);
            Assert.AreEqual(LanServerList.MaxEntries, list.Count);
        }
    }

    [TestFixture]
    public sealed class ServerEndpointTests
    {
        [Test]
        public void Parse_HostAndPort()
        {
            Assert.IsTrue(ServerEndpoint.TryParse(" 192.168.1.20:7800 ", out var h, out var p, out _));
            Assert.AreEqual("192.168.1.20", h);
            Assert.AreEqual(7800, (int)p);
        }

        [Test]
        public void Parse_DefaultPort()
        {
            Assert.IsTrue(ServerEndpoint.TryParse("sunucu.local", out var h, out var p, out _));
            Assert.AreEqual("sunucu.local", h);
            Assert.AreEqual((int)ServerEndpoint.DefaultPort, (int)p);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("1.2.3.4:0")]
        [TestCase("1.2.3.4:70000")]
        [TestCase("1.2.3.4:abc")]
        [TestCase(":7777")]
        [TestCase("a b:7777")]
        public void Parse_RejectsWithTurkishError(string raw)
        {
            Assert.IsFalse(ServerEndpoint.TryParse(raw, out _, out _, out var err));
            Assert.IsFalse(string.IsNullOrEmpty(err));
        }

        [Test]
        public void Port_Field()
        {
            Assert.IsTrue(ServerEndpoint.TryParsePort("", out var p));
            Assert.AreEqual(7777, (int)p);
            Assert.IsFalse(ServerEndpoint.TryParsePort("80", out _));
            Assert.IsTrue(ServerEndpoint.TryParsePort("9000", out p));
            Assert.AreEqual(9000, (int)p);
        }

        [Test]
        public void Recent_MovesToFrontDedupesAndCaps()
        {
            var s = "";
            for (var i = 1; i <= 8; i++)
                s = RecentServers.Add(s, "10.0.0." + i + ":7777");
            var list = RecentServers.Parse(s);
            Assert.AreEqual(RecentServers.Max, list.Count);
            Assert.AreEqual("10.0.0.8:7777", list[0]);
            s = RecentServers.Add(s, "10.0.0.5:7777");
            list = RecentServers.Parse(s);
            Assert.AreEqual("10.0.0.5:7777", list[0]);
            Assert.AreEqual(RecentServers.Max, list.Count);
            Assert.AreEqual(1, list.FindAll(x => x == "10.0.0.5:7777").Count);
        }

        [Test]
        public void Recent_ParseDropsInvalid()
        {
            var list = RecentServers.Parse("1.2.3.4:7777;;bad:port;1.2.3.4:7777;5.6.7.8");
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void FriendlyError_Turkish()
        {
            StringAssert.Contains("reddetti", ServerEndpoint.FriendlyError("Connection refused"));
            StringAssert.Contains("zaman", ServerEndpoint.FriendlyError("timed out"));
            Assert.IsNotEmpty(ServerEndpoint.FriendlyError(null));
        }
    }

    [TestFixture]
    public sealed class LobbyRulesTests
    {
        [Test]
        public void Ping_QualityAndText()
        {
            Assert.AreEqual(LobbyRules.PingQuality.Unknown, LobbyRules.Quality(-1));
            Assert.AreEqual(LobbyRules.PingQuality.Good, LobbyRules.Quality(40));
            Assert.AreEqual(LobbyRules.PingQuality.Ok, LobbyRules.Quality(100));
            Assert.AreEqual(LobbyRules.PingQuality.Bad, LobbyRules.Quality(300));
            Assert.AreEqual("—", LobbyRules.PingText(-1));
            Assert.AreEqual("999 ms", LobbyRules.PingText(5000));
        }

        [Test]
        public void Sort_HostThenReadyThenName()
        {
            var l = LobbyRules.Sort(new[]
            {
                new LobbyPlayer("Zeki", 10, false),
                new LobbyPlayer("Ali", 10, true),
                new LobbyPlayer("Komutan", 10, false, true),
                new LobbyPlayer("Bora", 10, true),
            });
            Assert.AreEqual("Komutan", l[0].Name);
            Assert.AreEqual("Ali", l[1].Name);
            Assert.AreEqual("Bora", l[2].Name);
            Assert.AreEqual("Zeki", l[3].Name);
            StringAssert.Contains("2 hazır", LobbyRules.Summary(l, 10));
        }
    }
}
