using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests
{
    public class AmbientLifeMathTests
    {
        [Test]
        public void TierCounts_AreMonotonicAndLowIsZero()
        {
            Assert.AreEqual(0, AmbientLifeMath.FlockCount(0));
            Assert.AreEqual(0, AmbientLifeMath.SmokeColumnCount(0));
            Assert.AreEqual(0, AmbientLifeMath.ButterflyCount(1));
            Assert.LessOrEqual(AmbientLifeMath.SmokeColumnCount(1), AmbientLifeMath.SmokeColumnCount(3));
            Assert.LessOrEqual(AmbientLifeMath.ButterflyCount(2), AmbientLifeMath.ButterflyCount(3));
        }

        [Test]
        public void BirdsPerFlock_IsFiveToNineAndDeterministic()
        {
            for (var s = 0; s < 50; s++)
                for (var f = 0; f < 3; f++)
                {
                    var n = AmbientLifeMath.BirdsPerFlock(f, s);
                    Assert.That(n, Is.InRange(5, 9));
                    Assert.AreEqual(n, AmbientLifeMath.BirdsPerFlock(f, s));
                }
        }

        [Test]
        public void SmokeLevel_DawnStrongNoonOffColdAlways()
        {
            Assert.Greater(AmbientLifeMath.SmokeLevel(3f, false), 0.9f);
            Assert.AreEqual(0f, AmbientLifeMath.SmokeLevel(50f, false), 1e-4f);
            Assert.GreaterOrEqual(AmbientLifeMath.SmokeLevel(50f, true), 0.85f);
        }

        [Test]
        public void ButterflyAndCricket_RespectDayNightWeather()
        {
            Assert.Greater(AmbientLifeMath.ButterflyLevel(60f, false, 0f), 0.95f);
            Assert.AreEqual(0f, AmbientLifeMath.ButterflyLevel(-10f, false, 0f), 1e-4f);
            Assert.AreEqual(0f, AmbientLifeMath.ButterflyLevel(60f, true, 0f), 1e-4f);
            Assert.AreEqual(0f, AmbientLifeMath.ButterflyLevel(60f, false, 1f), 1e-4f);
            Assert.Greater(AmbientLifeMath.CricketLevel(-20f, false, 0f), 0.95f);
            Assert.AreEqual(0f, AmbientLifeMath.CricketLevel(40f, false, 0f), 1e-4f);
        }

        [Test]
        public void GustBand_InRangeAndTravelsDownwind()
        {
            for (var i = 0; i < 200; i++)
            {
                var g = AmbientLifeMath.GustBand(i * 7f, i * 0.3f, 140f, 12f);
                Assert.That(g, Is.InRange(0f, 1f));
            }
            // Aynı dalga evresi: konum 12 m ilerleyip 1 sn geçince değer aynı kalır.
            Assert.AreEqual(AmbientLifeMath.GustBand(0f, 0f, 140f, 12f), AmbientLifeMath.GustBand(12f, 1f, 140f, 12f), 1e-3f);
        }

        [Test]
        public void PuffState_FadesInAndOut()
        {
            AmbientLifeMath.PuffState(0f, out _, out var a0, out _);
            AmbientLifeMath.PuffState(0.3f, out var s1, out var a1, out _);
            AmbientLifeMath.PuffState(1f, out var s2, out var a2, out _);
            Assert.AreEqual(0f, a0, 1e-4f);
            Assert.Greater(a1, 0.3f);
            Assert.AreEqual(0f, a2, 1e-4f);
            Assert.Greater(s2, s1);
        }

        [Test]
        public void ScatterDecay_ReachesZero()
        {
            var s = 1f;
            for (var i = 0; i < 100; i++) s = AmbientLifeMath.ScatterDecay(s, 0.1f);
            Assert.AreEqual(0f, s, 1e-4f);
        }

        [Test]
        public void TextureAlphas_AreInRange()
        {
            for (var u = 0f; u <= 1f; u += 0.05f)
                for (var v = 0f; v <= 1f; v += 0.05f)
                {
                    Assert.That(AmbientLifeMath.BirdAlpha(u, v), Is.InRange(0f, 1f));
                    Assert.That(AmbientLifeMath.PuffAlpha(u, v), Is.InRange(0f, 1f));
                    Assert.That(AmbientLifeMath.WingAlpha(u, v), Is.InRange(0f, 1f));
                }
            Assert.Greater(AmbientLifeMath.PuffAlpha(0.5f, 0.5f), 0.9f);
        }
    }
}
