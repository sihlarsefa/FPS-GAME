using NUnit.Framework;
using Project.Infrastructure.Audio.Weapons;

namespace Project.Tests.EditMode
{
    public sealed class ShotRealismTests
    {
        private static EnclosureProbe Room() => new EnclosureProbe { Forward = 6f, Back = 4f, Left = 3f, Right = 3f, Up = 3f, Absorption = 0.15f };

        [Test]
        public void Suppressor_ReducesBlast_ButSupersonicCrackRemains()
        {
            var sup = SuppressorRules.Mix(ShotClass.Rifle, MuzzleDevice.Suppressor, AmmoSpeed.Supersonic);
            var bare = SuppressorRules.Mix(ShotClass.Rifle, MuzzleDevice.None, AmmoSpeed.Supersonic);
            Assert.IsTrue(sup.BodyGain < 0.25f && sup.BodyGain > 0.1f);
            Assert.AreEqual(1f, bare.BodyGain, 0.001f);
            Assert.AreEqual(1f, sup.CrackGain, 0.001f);
        }

        [Test]
        public void SubsonicAmmo_HasNoCrack_AndExtraReduction()
        {
            var m = SuppressorRules.Mix(ShotClass.Rifle, MuzzleDevice.Suppressor, AmmoSpeed.Subsonic);
            Assert.AreEqual(0f, m.CrackGain, 0.001f);
            Assert.AreEqual(30f, SuppressorRules.NetReductionDb(ShotClass.Rifle, MuzzleDevice.Suppressor, AmmoSpeed.Subsonic, 0), 0.01f);
            Assert.AreEqual(26f, SuppressorRules.NetReductionDb(ShotClass.Rifle, MuzzleDevice.Suppressor, AmmoSpeed.Supersonic, 0), 0.01f);
        }

        [Test]
        public void Pistol_IsSubsonicByDefault()
        {
            Assert.IsFalse(SuppressorRules.IsSupersonic(SuppressorRules.DefaultMuzzleVelocity(ShotClass.Pistol)));
            Assert.IsTrue(SuppressorRules.IsSupersonic(SuppressorRules.DefaultMuzzleVelocity(ShotClass.Rifle)));
        }

        [Test]
        public void SuppressorWear_ReducesEfficiency()
        {
            var fresh = SuppressorRules.NetReductionDb(ShotClass.Smg, MuzzleDevice.Suppressor, AmmoSpeed.Subsonic, 0);
            var worn = SuppressorRules.NetReductionDb(ShotClass.Smg, MuzzleDevice.Suppressor, AmmoSpeed.Subsonic, 1500);
            Assert.AreEqual(5f, fresh - worn, 0.01f);
        }

        [Test]
        public void SuppressedMech_IsMoreProminent()
        {
            var sup = SuppressorRules.Mix(ShotClass.Pistol, MuzzleDevice.IntegralSuppressor, AmmoSpeed.Subsonic);
            var bare = SuppressorRules.Mix(ShotClass.Pistol, MuzzleDevice.None, AmmoSpeed.Subsonic);
            Assert.Greater(sup.MechGain, bare.MechGain);
            Assert.Less(sup.BodyCutoffScale, 1f);
        }

        [Test]
        public void Directivity_FrontLouderThanBack_BrakeBoostsSides()
        {
            Assert.AreEqual(0f, SuppressorRules.DirectivityDb(MuzzleDevice.None, 0f), 0.01f);
            Assert.AreEqual(-9f, SuppressorRules.DirectivityDb(MuzzleDevice.None, 180f), 0.01f);
            Assert.Greater(SuppressorRules.DirectivityDb(MuzzleDevice.Brake, 90f), SuppressorRules.DirectivityDb(MuzzleDevice.None, 90f) + 4f);
            Assert.Greater(SuppressorRules.DirectivityDb(MuzzleDevice.Suppressor, 180f), SuppressorRules.DirectivityDb(MuzzleDevice.None, 180f));
        }

        [Test]
        public void AudibleRange_SuppressedShorterButStillAudible()
        {
            var bare = SuppressorRules.Levels(ShotClass.Rifle, MuzzleDevice.None, AmmoSpeed.Supersonic);
            var sup = SuppressorRules.Levels(ShotClass.Rifle, MuzzleDevice.Suppressor, AmmoSpeed.Subsonic);
            var rBare = ShotPropagationRules.AudibleRange(bare.BlastDb, 40f);
            var rSup = ShotPropagationRules.AudibleRange(sup.BlastDb, 40f);
            Assert.Greater(rBare, 600f);
            Assert.Greater(rSup, 80f);
            Assert.Less(rSup, rBare / 3f);
        }

        [Test]
        public void AudibleRange_ShrinksInRain()
        {
            var lv = SuppressorRules.Levels(ShotClass.Smg, MuzzleDevice.Suppressor, AmmoSpeed.Subsonic);
            var dry = ShotPropagationRules.AudibleRange(lv.BlastDb, ShotPropagationRules.AmbientFromWeather(40f, 0f, 0f));
            var rain = ShotPropagationRules.AudibleRange(lv.BlastDb, ShotPropagationRules.AmbientFromWeather(40f, 1f, 6f));
            Assert.Less(rain, dry);
        }

        [Test]
        public void ArrivalDelay_FollowsSpeedOfSound_AndCaps()
        {
            Assert.AreEqual(1f, ShotPropagationRules.ArrivalDelay(343f), 0.02f);
            Assert.AreEqual(ShotPropagationRules.MaxGameDelaySeconds, ShotPropagationRules.ArrivalDelay(50000f), 0.001f);
            Assert.IsFalse(ShotPropagationRules.DelayWorthScheduling(10f));
            Assert.Greater(ShotPropagationRules.SpeedOfSound(30f), ShotPropagationRules.SpeedOfSound(0f));
        }

        [Test]
        public void Crack_ArrivesBeforeThump_AndMisleadsBearing()
        {
            var t = ShotPropagationRules.Crack(ShotClass.Rifle, 900f, 400f, 5f);
            Assert.IsTrue(t.Audible);
            Assert.AreEqual(0.67f, t.GapSeconds, 0.12f);
            Assert.Greater(t.ThumpSeconds, t.CrackSeconds);
            Assert.AreEqual(61f, t.BearingErrorDeg, 10f);
        }

        [Test]
        public void Crack_AbsentForSubsonic_OrFarOffPath()
        {
            Assert.IsFalse(ShotPropagationRules.Crack(ShotClass.Pistol, 330f, 100f, 3f).Audible);
            Assert.IsFalse(ShotPropagationRules.Crack(ShotClass.Rifle, 900f, 200f, 150f).Audible);
        }

        [Test]
        public void BulletFlight_DragSlowsBullet()
        {
            var k = ShotPropagationRules.DefaultDragK(ShotClass.Rifle);
            Assert.Less(ShotPropagationRules.BulletSpeedAt(900f, 600f, k), 900f);
            Assert.AreEqual(0.5f, ShotPropagationRules.TimeOfFlight(800f, 400f, 0f), 0.001f);
            Assert.Greater(ShotPropagationRules.TimeOfFlight(900f, 400f, k), 400f / 900f);
            Assert.AreEqual(90f, ShotPropagationRules.MachHalfAngleDeg(300f), 0.001f);
        }

        [Test]
        public void Transmission_LeakDominatesThickBarrier()
        {
            Assert.AreEqual(50f, AcousticTransmission.CombinedLossDb(BarrierMaterial.Concrete, 0f), 0.01f);
            Assert.AreEqual(13f, AcousticTransmission.CombinedLossDb(BarrierMaterial.Concrete, 0.05f), 0.4f);
            Assert.Greater(AcousticTransmission.CombinedCutoffHz(BarrierMaterial.Concrete, 0.05f), AcousticTransmission.CutoffHz(BarrierMaterial.Concrete));
        }

        [Test]
        public void Transmission_ChainAddsLossAndTakesLowestCutoff()
        {
            AcousticTransmission.Chain(new[] { BarrierMaterial.Window, BarrierMaterial.Brick }, out var loss, out var cut);
            Assert.AreEqual(62f, loss, 0.01f);
            Assert.AreEqual(900f, cut, 0.01f);
            Assert.AreEqual(0.5f, AcousticTransmission.PerceivedGain(10f), 0.001f);
        }

        [Test]
        public void Enclosure_OpenField_IsOpen()
        {
            var rep = EnclosureAnalyzer.Analyze(EnclosureProbe.Open());
            Assert.AreEqual(EnclosureKind.Open, rep.Kind);
            Assert.Greater(rep.Openness, 0.9f);
            var blend = EnclosureAnalyzer.Blend(rep);
            Assert.Greater(blend.Outdoor, 0.7f);
        }

        [Test]
        public void Enclosure_SmallRoom_IsIndoorWithModerateReverb()
        {
            var rep = EnclosureAnalyzer.Analyze(Room());
            Assert.AreEqual(EnclosureKind.Indoor, rep.Kind);
            Assert.Greater(rep.Rt60, 0.5f);
            Assert.Less(rep.Rt60, 2f);
            var b = EnclosureAnalyzer.Blend(rep);
            Assert.Greater(b.Indoor, 0.9f);
            Assert.AreEqual(1f, b.Outdoor + b.Indoor + b.Valley, 0.001f);
        }

        [Test]
        public void Enclosure_Canyon_FavoursValleyTail()
        {
            var p = new EnclosureProbe { Forward = 120f, Back = 120f, Left = 20f, Right = 30f, Up = 120f, Absorption = 0.15f };
            var rep = EnclosureAnalyzer.Analyze(p);
            Assert.AreEqual(EnclosureKind.Canyon, rep.Kind);
            Assert.Greater(EnclosureAnalyzer.Blend(rep).Valley, 0.7f);
        }

        [Test]
        public void Taps_OrderedByDelay_AndCapped()
        {
            var taps = EnclosureAnalyzer.Taps(Room(), 4);
            Assert.AreEqual(4, taps.Count);
            for (var i = 1; i < taps.Count; i++)
                Assert.IsTrue(taps[i].DelaySeconds >= taps[i - 1].DelaySeconds);
            Assert.AreEqual(2, EnclosureAnalyzer.Taps(Room(), 2).Count);
            Assert.AreEqual(0, EnclosureAnalyzer.Taps(EnclosureProbe.Open(), 4).Count);
        }

        [Test]
        public void Slapback_NeedsDistantWall_AndNotIndoors()
        {
            var open = EnclosureProbe.Open();
            float g;
            Assert.AreEqual(0f, EnclosureAnalyzer.SlapbackDelay(open, out g), 0.0001f);
            open.Forward = 100f;
            var delay = EnclosureAnalyzer.SlapbackDelay(open, out g);
            Assert.AreEqual(0.58f, delay, 0.03f);
            Assert.Greater(g, 0f);
            Assert.AreEqual(0f, EnclosureAnalyzer.SlapbackDelay(Room(), out g), 0.0001f);
        }

        [Test]
        public void Synth_SuppressedShot_IsFiniteAndBounded()
        {
            var o = SuppressedShotSynth.RenderShot(ShotClass.Rifle, MuzzleDevice.Suppressor, AmmoSpeed.Supersonic, 60f, Room(), false, 1);
            Assert.Greater(o.Length, 1000);
            var peak = 0f;
            for (var i = 0; i < o.Length; i++)
            {
                Assert.IsTrue(!float.IsNaN(o[i]) && !float.IsInfinity(o[i]));
                if (System.Math.Abs(o[i]) > peak) peak = System.Math.Abs(o[i]);
            }
            Assert.IsTrue(peak > 0.2f && peak <= 1.001f);
        }

        [Test]
        public void Synth_Crack_ShorterThanBody()
        {
            Assert.Less(SuppressedShotSynth.RenderCrack(ShotClass.Rifle, 0).Length, SuppressedShotSynth.RenderBody(ShotClass.Rifle, MuzzleDevice.Suppressor, 0).Length);
        }
    }
}
