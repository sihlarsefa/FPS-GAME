#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Ambience;
using Project.Infrastructure.Audio.HdrMix;

namespace Project.Tests.EditMode
{
    public sealed class AudioHdrMixTests
    {
        [Test]
        public void Hdr_QuietSoundsPushedDownAfterLoudEvent()
        {
            var w = new HdrWindow();
            var quiet = -70f;
            Assert.AreEqual(1f, w.GainLinear(quiet), 0.001f);
            w.Report(0f);
            Assert.IsTrue(w.GainLinear(quiet) < 0.2f);
            Assert.AreEqual(1f, w.GainLinear(-20f), 0.001f);
        }

        [Test]
        public void Hdr_ReleasesOverTime()
        {
            var w = new HdrWindow();
            w.Report(0f);
            var g0 = w.GainLinear(-70f);
            for (var i = 0; i < 20; i++) w.Tick(0.5f);
            Assert.IsTrue(w.GainLinear(-70f) > g0);
            Assert.AreEqual(1f, w.GainLinear(-70f), 0.001f);
            Assert.AreEqual(HdrAudioMath.IdleTopDb, w.TopDb, 0.001f);
        }

        [Test]
        public void Hdr_SimultaneousEventsSumAndCapped()
        {
            var w = new HdrWindow();
            w.Report(0f);
            w.Report(0f);
            Assert.IsTrue(w.TopDb > 2.5f && w.TopDb <= HdrAudioMath.MaxTopDb);
            Assert.IsTrue(w.HeadroomLinear < 1f);
            w.Report(40f);
            Assert.IsTrue(w.TopDb <= HdrAudioMath.MaxTopDb + 0.001f);
        }

        [Test]
        public void Hdr_PushIsBoundedAndMonotonic()
        {
            Assert.AreEqual(0f, HdrAudioMath.PushDb(-10f, 0f), 0.001f);
            Assert.IsTrue(HdrAudioMath.PushDb(-60f, 0f) < HdrAudioMath.PushDb(-45f, 0f));
            Assert.AreEqual(-HdrAudioMath.MaxPushDb, HdrAudioMath.PushDb(-200f, 0f), 0.001f);
            Assert.AreEqual(3.01f, HdrAudioMath.PowerSumDb(0f, 0f), 0.02f);
        }

        [Test]
        public void Hdr_EventLevels_DistanceAndWeapons()
        {
            Assert.IsTrue(HdrAudioMath.EventLevelDb(HdrEventKind.Explosion, 5f) > HdrAudioMath.EventLevelDb(HdrEventKind.Explosion, 80f));
            Assert.IsTrue(HdrAudioMath.EventLevelDb(HdrEventKind.OwnMachineGun, 1f) > HdrAudioMath.EventLevelDb(HdrEventKind.OwnPistol, 1f));
            Assert.AreEqual(HdrEventKind.OwnMachineGun, HdrAudioMath.KindForWeaponId("lmg_pmt76"));
            Assert.AreEqual(HdrEventKind.Sniper, HdrAudioMath.KindForWeaponId("sr_jng90"));
            Assert.AreEqual(HdrEventKind.OwnRifle, HdrAudioMath.KindForWeaponId("ar_mpt55"));
            Assert.AreEqual(HdrEventKind.Generic, HdrAudioMath.KindForWeaponId(null));
        }

        [Test]
        public void Tinnitus_SeverityDurationEnvelope()
        {
            Assert.IsTrue(MixSnapshotMath.TinnitusSeverity(2f, 8f) > MixSnapshotMath.TinnitusSeverity(10f, 8f));
            Assert.AreEqual(0f, MixSnapshotMath.TinnitusSeverity(40f, 8f), 0.0001f);
            Assert.AreEqual(0f, MixSnapshotMath.TinnitusDuration(0.05f), 0.0001f);
            var dur = MixSnapshotMath.TinnitusDuration(1f);
            Assert.IsTrue(dur >= 3f && dur <= 6f);
            Assert.IsTrue(MixSnapshotMath.TinnitusDuration(0.5f) < dur);
            Assert.AreEqual(1f, MixSnapshotMath.TinnitusEnvelope(0.5f, 4f), 0.0001f);
            Assert.AreEqual(0f, MixSnapshotMath.TinnitusEnvelope(4.1f, 4f), 0.0001f);
            Assert.IsTrue(MixSnapshotMath.TinnitusSeverity(5f, 8f, true) < MixSnapshotMath.TinnitusSeverity(5f, 8f, false));
        }

        [Test]
        public void MixState_TinnitusMuffleThenRecovers()
        {
            var s = new MixSnapshotState();
            Assert.IsTrue(s.TriggerTinnitus(1f) >= 3f);
            for (var i = 0; i < 30; i++) s.Tick(0.05f);
            Assert.IsTrue(s.Current.LowpassHz < 3000f);
            Assert.IsTrue(s.Current.TinnitusGain > 0.5f);
            for (var i = 0; i < 400; i++) s.Tick(0.05f);
            Assert.IsFalse(s.TinnitusActive);
            Assert.IsTrue(s.Current.LowpassHz > 15000f);
            Assert.IsTrue(s.Current.TinnitusGain < 0.05f);
            Assert.AreEqual(1f, s.Current.MasterGain, 0.05f);
        }

        [Test]
        public void MixState_SnapshotsCombine()
        {
            var s = new MixSnapshotState { Underwater = true, Indoor = true };
            var t = s.ComputeTarget();
            Assert.IsTrue(t.LowpassHz <= MixSnapshotMath.Target(MixSnapshotKind.Underwater).LowpassHz + 0.01f);
            Assert.IsTrue(t.ReverbSend > 0f);
            Assert.IsTrue(t.AmbienceGain < MixSnapshotMath.Target(MixSnapshotKind.Indoor).AmbienceGain);
            var dead = new MixSnapshotState { Dead = true }.ComputeTarget();
            Assert.IsTrue(dead.LowpassHz < 500f);
            var ads = new MixSnapshotState { Ads = true }.ComputeTarget();
            Assert.IsTrue(ads.AmbienceGain < 1f && ads.AmbienceGain > 0.5f);
            Assert.AreEqual(MixSnapshotMath.OpenLowpassHz, ads.LowpassHz, 0.01f);
        }

        [Test]
        public void MixState_ResetAll()
        {
            var s = new MixSnapshotState { Dead = true, Ads = true };
            s.TriggerTinnitus(1f);
            s.ResetAll();
            Assert.IsFalse(s.TinnitusActive);
            Assert.AreEqual(1f, s.ComputeTarget().MasterGain, 0.0001f);
        }

        [Test]
        public void Ambience_NightHasInsectsOwlsDay_HasBirds()
        {
            var day = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.DagCam, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Acik });
            var night = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.DagCam, Time = TimeOfDay.Gece, Weather = WeatherKind.Acik });
            Assert.IsTrue(day.Rate(AmbienceOneShot.Bird) > 0f);
            Assert.AreEqual(0f, night.Rate(AmbienceOneShot.Bird), 0.0001f);
            Assert.IsTrue(night.Rate(AmbienceOneShot.Owl) > 0f);
            Assert.IsTrue(night.Loop(AmbienceLoop.Insects) > day.Loop(AmbienceLoop.Insects));
        }

        [Test]
        public void Ambience_RainOutdoorVsRoof()
        {
            var outd = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.Yayla, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Yagmur });
            var ind = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.Yayla, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Yagmur, Indoor = true });
            Assert.IsTrue(outd.Loop(AmbienceLoop.RainOutdoor) > 0.5f);
            Assert.AreEqual(0f, outd.Loop(AmbienceLoop.RainRoof), 0.0001f);
            Assert.IsTrue(ind.Loop(AmbienceLoop.RainRoof) > ind.Loop(AmbienceLoop.RainOutdoor));
            Assert.IsTrue(ind.Loop(AmbienceLoop.WindGust) < outd.Loop(AmbienceLoop.WindGust));
            Assert.IsTrue(ind.OneShotVolume < outd.OneShotVolume);
            Assert.AreEqual(0f, outd.Loop(AmbienceLoop.Insects), 0.0001f);
        }

        [Test]
        public void Ambience_VillageAndBiomeSpecific()
        {
            var v = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.Yayla, Time = TimeOfDay.Safak, Weather = WeatherKind.Acik, NearVillage = true });
            var nv = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.Yayla, Time = TimeOfDay.Safak, Weather = WeatherKind.Acik, NearVillage = false });
            Assert.IsTrue(v.Rate(AmbienceOneShot.Rooster) > 0f);
            Assert.AreEqual(0f, nv.Rate(AmbienceOneShot.Rooster), 0.0001f);
            Assert.IsTrue(v.Rate(AmbienceOneShot.DogBark) > nv.Rate(AmbienceOneShot.DogBark));
            var coast = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.Kiyi, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Acik });
            Assert.IsTrue(coast.Loop(AmbienceLoop.Waves) > 0f);
            Assert.IsTrue(coast.Rate(AmbienceOneShot.Gull) > 0f);
            var snow = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.Kar, Time = TimeOfDay.Gece, Weather = WeatherKind.Kar });
            Assert.IsTrue(snow.Loop(AmbienceLoop.SnowWind) > 0f);
            Assert.AreEqual(0f, snow.Loop(AmbienceLoop.Insects), 0.0001f);
            Assert.AreEqual(AmbienceBiome.Kar, AmbienceRules.BiomeForMap(MapCatalog.AyazGecidi));
            Assert.AreEqual(AmbienceBiome.DagCam, AmbienceRules.BiomeForMap("bilinmeyen"));
        }

        [Test]
        public void OneShotScheduler_RespectsRatesAndGaps()
        {
            var plan = new AmbiencePlan();
            plan.OneShotPerMinute[(int)AmbienceOneShot.Bird] = 600f; // çok sık
            var s = new AmbienceOneShotScheduler(42u);
            var count = 0;
            var last = -100f;
            for (var t = 0f; t < 60f; t += 0.1f)
            {
                if (s.TryTick(plan, t, 0.1f, out var shot))
                {
                    count++;
                    Assert.AreEqual(AmbienceOneShot.Bird, shot.Kind);
                    Assert.IsTrue(t - last >= 4.99f);
                    Assert.IsTrue(shot.Distance > 0f && shot.Volume > 0f);
                    last = t;
                }
            }

            Assert.IsTrue(count >= 8 && count <= 12);
            var empty = new AmbienceOneShotScheduler(1u);
            Assert.IsFalse(empty.TryTick(new AmbiencePlan(), 1f, 0.1f, out _));
        }

        [Test]
        public void Distant_RealEventsDelayedBySpeedOfSound()
        {
            var d = new DistantBattleScheduler(5u) { SyntheticEnabled = false };
            Assert.IsFalse(d.OnRealEvent(DistantKind.Gunshot, 50f, 0f, 0f), "çok yakın");
            Assert.IsFalse(d.OnRealEvent(DistantKind.Gunshot, 5000f, 0f, 0f), "çok uzak");
            Assert.IsTrue(d.OnRealEvent(DistantKind.Blast, 686f, 90f, 10f));
            Assert.IsFalse(d.TryPop(11f, out _));
            Assert.IsTrue(d.TryPop(12.1f, out var e));
            Assert.AreEqual(DistantKind.Blast, e.Kind);
            Assert.AreEqual(90f, e.AngleDeg, 0.001f);
            Assert.IsFalse(e.Synthetic);
            Assert.IsTrue(e.Volume > 0f && e.LowpassHz < 5300f);
        }

        [Test]
        public void Distant_ThinsAutomaticFireAndBoundsQueue()
        {
            var d = new DistantBattleScheduler(5u) { SyntheticEnabled = false };
            var accepted = 0;
            for (var i = 0; i < 100; i++)
                if (d.OnRealEvent(DistantKind.Gunshot, 300f, 0f, 1f + i * 0.01f)) accepted++;
            Assert.IsTrue(accepted <= 10);
            for (var i = 0; i < 100; i++)
                d.OnRealEvent(DistantKind.Blast, 200f + i * 5f, 0f, 2f);
            Assert.IsTrue(d.Count <= DistantBattleScheduler.MaxQueue);
        }

        [Test]
        public void Distant_SyntheticFallbackOnlyWithoutRealEvents()
        {
            var d = new DistantBattleScheduler(9u);
            var synth = 0;
            for (var t = 0f; t < 200f; t += 0.1f)
                while (d.TryPop(t, out var e))
                {
                    Assert.IsTrue(e.Synthetic);
                    synth++;
                }

            Assert.IsTrue(synth > 3);

            var busy = new DistantBattleScheduler(9u);
            var syntheticSeen = 0;
            for (var t = 0f; t < 60f; t += 0.5f)
            {
                busy.OnRealEvent(DistantKind.Blast, 600f, 0f, t);
                while (busy.TryPop(t + 5f, out var e))
                    if (e.Synthetic) syntheticSeen++;
            }

            Assert.AreEqual(0, syntheticSeen);
        }

        [Test]
        public void Distant_VolumeAndFilterFallWithDistance()
        {
            Assert.IsTrue(DistantBattleScheduler.VolumeFor(DistantKind.Gunshot, 200f) > DistantBattleScheduler.VolumeFor(DistantKind.Gunshot, 1200f));
            Assert.IsTrue(DistantBattleScheduler.LowpassFor(200f) > DistantBattleScheduler.LowpassFor(1200f));
            Assert.AreEqual(1f, DistantBattleScheduler.DelayFor(343f), 0.001f);
        }

        [Test]
        public void Synth_ProducesLoopsAndShots()
        {
            foreach (AmbienceLoop l in System.Enum.GetValues(typeof(AmbienceLoop)))
            {
                var b = AmbienceSynth.RenderLoop(l);
                Assert.IsTrue(b != null && b.Length > 44100, l.ToString());
                AssertFiniteAndAudible(b, l.ToString());
            }

            foreach (AmbienceOneShot k in System.Enum.GetValues(typeof(AmbienceOneShot)))
            {
                if (k == AmbienceOneShot.DistantGun || k == AmbienceOneShot.DistantBlast)
                {
                    Assert.IsNull(AmbienceSynth.RenderOneShot(k, 0));
                    continue;
                }

                var b = AmbienceSynth.RenderOneShot(k, 1);
                Assert.IsTrue(b != null && b.Length > 4000, k.ToString());
                AssertFiniteAndAudible(b, k.ToString());
            }

            var ring = MixSynth.RenderTinnitusRing();
            AssertFiniteAndAudible(ring, "ring");
        }

        private static void AssertFiniteAndAudible(float[] b, string name)
        {
            var max = 0f;
            for (var i = 0; i < b.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(b[i]) || float.IsInfinity(b[i]), name + " NaN");
                var a = b[i] < 0 ? -b[i] : b[i];
                if (a > max) max = a;
            }

            Assert.IsTrue(max > 0.05f && max <= 1.01f, name + " seviye " + max);
        }

        [Test]
        public void MixState_IdleIsFullyOpen_AndAdsDoesNotMuffle()
        {
            var s = new MixSnapshotState();
            for (var i = 0; i < 10; i++) s.Tick(0.016f);
            Assert.AreEqual(22000f, s.Current.LowpassHz, 0.5f);
            s.Ads = true;
            for (var i = 0; i < 60; i++) s.Tick(0.016f);
            Assert.AreEqual(22000f, s.Current.LowpassHz, 0.5f);
        }

        [Test]
        public void MixState_DownedReleasesWithinOneAndHalfSeconds()
        {
            var s = new MixSnapshotState { Downed = true };
            for (var i = 0; i < 60; i++) s.Tick(0.016f);
            Assert.Less(s.Current.LowpassHz, 3000f);
            s.Downed = false;
            var t = 0f;
            while (s.Current.LowpassHz < 21999f && t < 5f) { s.Tick(0.016f); t += 0.016f; }
            Assert.Less(t, 1.5f);
            Assert.AreEqual(22000f, s.Current.LowpassHz, 0.5f);
        }

        [Test]
        public void Hdr_ReleasesFromPeakInUnderOneSecond()
        {
            var w = new HdrWindow();
            w.Report(6f);
            var t = 0f;
            while (w.TopDb > HdrAudioMath.IdleTopDb + 0.01f && t < 5f) { w.Tick(0.016f); t += 0.016f; }
            Assert.Less(t, 1.2f);
        }
    }
}
#endif
