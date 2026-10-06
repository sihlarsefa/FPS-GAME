using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Ambience;

namespace Project.Tests
{
    public sealed class AmbienceBedsTests
    {
        private static AmbienceContext Ctx(AmbienceBiome b, TimeOfDay t, WeatherKind w, bool baseZone = false) =>
            new AmbienceContext { Biome = b, Time = t, Weather = w, NearBase = baseZone };

        [Test]
        public void StrongWind_OnlyInWindyBiomes()
        {
            var snow = AmbienceRules.Build(Ctx(AmbienceBiome.Kar, TimeOfDay.Gunduz, WeatherKind.Kar));
            var forest = AmbienceRules.Build(Ctx(AmbienceBiome.DagCam, TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.Greater(snow.Loop(AmbienceLoop.WindStrong), 0.3f);
            Assert.AreEqual(0f, forest.Loop(AmbienceLoop.WindStrong));
        }

        [Test]
        public void Grasshopper_DayOnlyAndNotInRain()
        {
            Assert.Greater(AmbienceRules.Build(Ctx(AmbienceBiome.DagCam, TimeOfDay.Gunduz, WeatherKind.Acik)).Loop(AmbienceLoop.Grasshopper), 0f);
            Assert.AreEqual(0f, AmbienceRules.Build(Ctx(AmbienceBiome.DagCam, TimeOfDay.Gece, WeatherKind.Acik)).Loop(AmbienceLoop.Grasshopper));
            Assert.AreEqual(0f, AmbienceRules.Build(Ctx(AmbienceBiome.DagCam, TimeOfDay.Gunduz, WeatherKind.Yagmur)).Loop(AmbienceLoop.Grasshopper));
        }

        [Test]
        public void RadioHiss_OnlyNearBase()
        {
            Assert.AreEqual(0f, AmbienceRules.Build(Ctx(AmbienceBiome.DagCam, TimeOfDay.Gece, WeatherKind.Acik)).Loop(AmbienceLoop.RadioHiss));
            Assert.Greater(AmbienceRules.Build(Ctx(AmbienceBiome.DagCam, TimeOfDay.Gece, WeatherKind.Acik, true)).Loop(AmbienceLoop.RadioHiss), 0f);
        }

        [Test]
        public void CannonTimer_IntervalIsFortyToNinety()
        {
            var t = new DistantCannonTimer(5u);
            Assert.IsTrue(!t.TryTick(0f, out _));
            var last = 0f;
            var now = 0f;
            var fired = 0;
            while (now < 2000f && fired < 8)
            {
                now += 0.5f;
                if (t.TryTick(now, out var e))
                {
                    if (fired > 0)
                    {
                        Assert.IsTrue(now - last >= 39.5f);
                        Assert.IsTrue(now - last <= 90.6f);
                    }

                    Assert.IsTrue(e.Kind == DistantKind.Blast);
                    last = now;
                    fired++;
                }
            }

            Assert.Greater(fired, 5);
        }

        [Test]
        public void Crossfade_OutAndInAreComplementary()
        {
            Assert.AreEqual(1f, BedCrossfade.Out(0f));
            Assert.AreEqual(0f, BedCrossfade.Out(0.5f));
            Assert.AreEqual(0f, BedCrossfade.In(0.5f));
            Assert.AreEqual(1f, BedCrossfade.In(1f));
            BedCrossfade.EqualPower(0.5f, out var o, out var i);
            Assert.AreEqual(1f, o * o + i * i, 0.001f);
        }
    }
}
