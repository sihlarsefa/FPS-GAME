using System.IO;
using NUnit.Framework;
using Project.Application.Replay;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class ReplayCodecTests
    {
        private static ReplayData Sample()
        {
            var d = new ReplayData { MapId = "kuzgun", MatchSeed = 42, WorldSeed = 1923, LocalPlayerId = 3, RecordedAtUtcTicks = 123456789L };
            d.Players.Add(new ReplayPlayer { Id = 3, Name = "Çavuş Demir", Team = 0, IsBot = false });
            d.Players.Add(new ReplayPlayer { Id = 12, Name = "Bot-12", Team = 2, IsBot = true });
            var ak = d.WeaponIndex("ak");
            for (var i = 0; i < 5; i++)
            {
                var f = new ReplayFrame { Time = i * 0.1f, ZoneX = 10f, ZoneZ = -20f, ZoneRadius = 500f - i };
                f.Actors = new[]
                {
                    new ReplayActorSample { Id = 3, X = i, Y = 1.5f, Z = -i * 2f, Yaw = 350f + i * 4f, Pitch = -30f, Stance = 1, Weapon = ak, Alive = true },
                    new ReplayActorSample { Id = 12, X = 100f, Y = 0f, Z = 50f, Yaw = 90f, Pitch = 10f, Stance = 2, Weapon = -1, Alive = i < 3 }
                };
                d.Frames.Add(f);
            }
            d.Events.Add(new ReplayEvent { Time = 0.2f, Type = ReplayEventType.Shot, Actor = 3, Target = -1, X = 1, Y = 2, Z = 3, Weapon = ak });
            d.Events.Add(new ReplayEvent { Time = 0.3f, Type = ReplayEventType.Death, Actor = 3, Target = 12, X = 100, Y = 0, Z = 50, Weapon = ak, Flag = true });
            return d;
        }

        [Test]
        public void RoundTrip_PreservesHeaderTablesAndEvents()
        {
            var back = ReplayCodec.FromBytes(ReplayCodec.ToBytes(Sample()));
            Assert.AreEqual("kuzgun", back.MapId);
            Assert.AreEqual(42, back.MatchSeed);
            Assert.AreEqual(1923, back.WorldSeed);
            Assert.AreEqual(3, back.LocalPlayerId);
            Assert.AreEqual(123456789L, back.RecordedAtUtcTicks);
            Assert.AreEqual(2, back.Players.Count);
            Assert.AreEqual("Çavuş Demir", back.Players[0].Name);
            Assert.IsTrue(back.Players[1].IsBot);
            Assert.AreEqual("ak", back.WeaponName(0));
            Assert.AreEqual(5, back.Frames.Count);
            Assert.AreEqual(2, back.Events.Count);
            Assert.AreEqual(ReplayEventType.Death, back.Events[1].Type);
            Assert.AreEqual(3, back.Events[1].Actor);
            Assert.AreEqual(12, back.Events[1].Target);
            Assert.IsTrue(back.Events[1].Flag);
            Assert.AreEqual(-1, back.Events[0].Target);
        }

        [Test]
        public void RoundTrip_QuantizedAnglesWithinTolerance()
        {
            var src = Sample();
            var back = ReplayCodec.FromBytes(ReplayCodec.ToBytes(src));
            var s = back.Frames[2].Actors[0];
            Assert.AreEqual(src.Frames[2].Actors[0].Yaw, s.Yaw, 0.02f);
            Assert.AreEqual(-30f, s.Pitch, 0.5f);
            Assert.AreEqual(1, s.Stance);
            Assert.IsTrue(s.Alive);
            Assert.AreEqual(0, s.Weapon);
            var b = back.Frames[4].Actors[1];
            Assert.IsFalse(b.Alive);
            Assert.AreEqual(-1, b.Weapon);
            Assert.AreEqual(2, b.Stance);
        }

        [Test]
        public void Gzip_RoundTripAndSmallerOrEqualForRepetitiveData()
        {
            var d = Sample();
            var raw = ReplayCodec.ToBytes(d, false);
            var gz = ReplayCodec.ToBytes(d, true);
            Assert.AreEqual(0x1F, gz[0]);
            var back = ReplayCodec.FromBytes(gz);
            Assert.AreEqual(raw.Length, ReplayCodec.ToBytes(back, false).Length);
        }

        [Test]
        public void BadMagic_Throws()
        {
            Assert.Throws<InvalidDataException>(() => ReplayCodec.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6 }));
        }

        [Test]
        public void Empty_Throws()
        {
            Assert.Throws<InvalidDataException>(() => ReplayCodec.FromBytes(new byte[0]));
        }

        [Test]
        public void Truncated_Throws()
        {
            var bytes = ReplayCodec.ToBytes(Sample());
            var cut = new byte[bytes.Length / 2];
            System.Array.Copy(bytes, cut, cut.Length);
            Assert.Throws<EndOfStreamException>(() => ReplayCodec.FromBytes(cut));
        }

        [Test]
        public void VarInt_RoundTrips()
        {
            foreach (var v in new[] { 0, 1, 127, 128, 300, 16384, 2000000 })
            {
                var ms = new MemoryStream();
                var w = new BinaryWriter(ms);
                ReplayCodec.WriteVar(w, v);
                w.Flush();
                ms.Position = 0;
                Assert.AreEqual(v, ReplayCodec.ReadVar(new BinaryReader(ms)));
            }
        }

        [Test]
        public void YawQuantization_Wraps()
        {
            Assert.AreEqual(350f, ReplayCodec.DequantizeYaw(ReplayCodec.QuantizeYaw(-10f)), 0.01f);
            Assert.AreEqual(0f, ReplayCodec.DequantizeYaw(ReplayCodec.QuantizeYaw(360f)), 0.01f);
        }

        [Test]
        public void Duration_AndKills()
        {
            var d = Sample();
            Assert.AreEqual(0.4f, d.Duration, 1e-4f);
            Assert.AreEqual(1, d.Kills().Count);
            Assert.AreEqual(0, new ReplayData().Duration);
        }

        [Test]
        public void WeaponIndex_DeduplicatesAndHandlesEmpty()
        {
            var d = new ReplayData();
            Assert.AreEqual(-1, d.WeaponIndex(null));
            Assert.AreEqual(0, d.WeaponIndex("a"));
            Assert.AreEqual(1, d.WeaponIndex("b"));
            Assert.AreEqual(0, d.WeaponIndex("a"));
        }

        [Test]
        public void Timeline_FindFrame_Bounds()
        {
            var d = Sample();
            Assert.AreEqual(-1, ReplayTimeline.FindFrame(new ReplayFrame[0], 1f));
            Assert.AreEqual(0, ReplayTimeline.FindFrame(d.Frames, -5f));
            Assert.AreEqual(4, ReplayTimeline.FindFrame(d.Frames, 99f));
            Assert.AreEqual(2, ReplayTimeline.FindFrame(d.Frames, 0.25f));
        }

        [Test]
        public void Timeline_LerpAngle_ShortestPath()
        {
            Assert.AreEqual(0f, ReplayTimeline.LerpAngle(350f, 10f, 0.5f), 0.01f);
            Assert.AreEqual(350f, ReplayTimeline.LerpAngle(10f, 350f, 1f), 0.01f);
        }

        [Test]
        public void Timeline_Sample_Interpolates()
        {
            var d = Sample();
            Assert.IsTrue(ReplayTimeline.Sample(d, 3, 0.15f, out var s));
            Assert.AreEqual(1.5f, s.X, 1e-3f);
            Assert.IsFalse(ReplayTimeline.Sample(d, 999, 0.15f, out _));
        }

        [Test]
        public void Timeline_FirstEventAtOrAfter()
        {
            var d = Sample();
            Assert.AreEqual(0, ReplayTimeline.FirstEventAtOrAfter(d.Events, 0f));
            Assert.AreEqual(1, ReplayTimeline.FirstEventAtOrAfter(d.Events, 0.25f));
            Assert.AreEqual(2, ReplayTimeline.FirstEventAtOrAfter(d.Events, 5f));
        }
    }
}
