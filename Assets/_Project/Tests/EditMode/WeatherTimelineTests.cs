using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class WeatherTimelineTests
    {
        [Test]
        public void SameSeed_SameTimeline()
        {
            var a = new WeatherTimeline(1234);
            var b = new WeatherTimeline(1234);
            Assert.AreEqual(a.FirstRainStart, b.FirstRainStart);
            Assert.AreEqual(a.Strikes.Count, b.Strikes.Count);
            for (float t = 0; t < 1500; t += 37f)
                Assert.AreEqual(a.Sample(t).Rain, b.Sample(t).Rain);
        }

        [Test]
        public void DifferentSeed_DiffersAndRainComes()
        {
            Assert.AreNotEqual(new WeatherTimeline(1).FirstRainStart, new WeatherTimeline(2).FirstRainStart);
            for (int seed = 0; seed < 20; seed++)
                Assert.Greater(new WeatherTimeline(seed).FirstRainStart, 0f);
        }

        [Test]
        public void StartsClear_RainsLater_ClearsAgain()
        {
            var w = new WeatherTimeline(77);
            Assert.AreEqual(0f, w.Sample(0f).Rain);
            Assert.AreEqual(0f, w.Sample(0f).Cloud);
            Assert.AreEqual(1f, w.Sample(w.FirstRainStart + 100f).Rain, 1e-4f);
            Assert.AreEqual(0f, w.Sample(w.FirstRainStart - 1f).Rain);
            Assert.Greater(w.Sample(w.FirstRainStart - 1f).Cloud, 0.9f);
            Assert.Greater(w.Sample(w.FirstRainStart - 1f).Curtain, 0.9f);
        }

        [Test]
        public void Transitions_AreSmooth()
        {
            var w = new WeatherTimeline(5);
            var prev = w.Sample(0f);
            for (float t = 0.5f; t < 1500f; t += 0.5f)
            {
                var s = w.Sample(t);
                Assert.Less(System.Math.Abs(s.Cloud - prev.Cloud), 0.08f, "cloud @" + t);
                Assert.Less(System.Math.Abs(s.Rain - prev.Rain), 0.12f, "rain @" + t);
                Assert.That(s.Rain, Is.InRange(0f, 1f));
                prev = s;
            }
        }

        [Test]
        public void Strikes_OnlyInRain_SortedAndInRange()
        {
            var w = new WeatherTimeline(9);
            Assert.Greater(w.Strikes.Count, 0);
            float last = -1f;
            foreach (var st in w.Strikes)
            {
                Assert.GreaterOrEqual(st.Time, last);
                last = st.Time;
                Assert.Greater(w.Sample(st.Time).Rain, 0.5f);
                Assert.That(st.DistanceM, Is.InRange(100f, 3000f));
            }
            var list = new List<LightningStrike>();
            w.StrikesBetween(0f, 99999f, list);
            Assert.AreEqual(w.Strikes.Count, list.Count);
        }

        [Test]
        public void Thunder_DelayAndVolume()
        {
            Assert.AreEqual(1f, WeatherTimeline.ThunderDelaySeconds(343f), 1e-4f);
            Assert.Greater(WeatherTimeline.ThunderVolume(100f), WeatherTimeline.ThunderVolume(2500f));
            Assert.Greater(WeatherTimeline.ThunderVolume(99999f), 0.1f);
        }

        [Test]
        public void FlashEnvelope_PeaksThenDies()
        {
            Assert.AreEqual(0f, WeatherTimeline.FlashEnvelope(-1f));
            Assert.AreEqual(1f, WeatherTimeline.FlashEnvelope(0.06f), 1e-3f);
            Assert.AreEqual(0f, WeatherTimeline.FlashEnvelope(1f));
        }

        [Test]
        public void KindFor_HasHysteresis()
        {
            Assert.AreEqual(WeatherKind.Acik, WeatherTimeline.KindFor(0.2f, WeatherKind.Acik));
            Assert.AreEqual(WeatherKind.Yagmur, WeatherTimeline.KindFor(0.35f, WeatherKind.Acik));
            Assert.AreEqual(WeatherKind.Yagmur, WeatherTimeline.KindFor(0.2f, WeatherKind.Yagmur));
            Assert.AreEqual(WeatherKind.Acik, WeatherTimeline.KindFor(0.05f, WeatherKind.Yagmur));
        }

        [Test]
        public void WindStrength_RisesWithStorm()
        {
            Assert.Greater(WeatherTimeline.WindStrength(new WeatherSample { Cloud = 1, Rain = 1, Storm = 1 }),
                WeatherTimeline.WindStrength(new WeatherSample()));
        }
    }
}
