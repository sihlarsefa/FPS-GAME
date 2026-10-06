using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Audio.Foley;
using Project.Infrastructure.Audio.Weapons;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class FootstepRealismTests
    {
        [Test]
        public void Level_CombinesSurfaceGaitAndLoad()
        {
            Assert.AreEqual(70f, FootstepRules.LevelDb(FootSurface.Concrete, Gait.Walk, 0f), 0.01f);
            Assert.AreEqual(83f, FootstepRules.LevelDb(FootSurface.Metal, Gait.Sprint, 0f), 0.01f);
            Assert.AreEqual(48f, FootstepRules.LevelDb(FootSurface.Grass, Gait.Prone, 0f), 0.01f);
            Assert.AreEqual(1.94f, FootstepRules.LoadDb(20f), 0.05f);
        }

        [Test]
        public void Wetness_RaisesSensitiveSurfacesOnly()
        {
            var dry = FootstepRules.LevelDb(FootSurface.Grass, Gait.Walk, 0f, 0f);
            var wet = FootstepRules.LevelDb(FootSurface.Grass, Gait.Walk, 0f, 1f);
            Assert.Greater(wet, dry + 1.5f);
        }

        [Test]
        public void Gait_FollowsSpeedAndPosture()
        {
            Assert.AreEqual(Gait.Walk, FootstepRules.GaitFor(2f, false, false));
            Assert.AreEqual(Gait.Jog, FootstepRules.GaitFor(4f, false, false));
            Assert.AreEqual(Gait.Sprint, FootstepRules.GaitFor(5.5f, false, false));
            Assert.AreEqual(Gait.CrouchWalk, FootstepRules.GaitFor(5.5f, true, false));
            Assert.AreEqual(Gait.Prone, FootstepRules.GaitFor(1f, true, true));
        }

        [Test]
        public void AudibleRange_ScalesWithGait()
        {
            var walk = FootstepRules.AudibleRange(FootstepRules.LevelDb(FootSurface.Concrete, Gait.Walk, 0f));
            var sprint = FootstepRules.AudibleRange(FootstepRules.LevelDb(FootSurface.Concrete, Gait.Sprint, 0f));
            var crouch = FootstepRules.AudibleRange(FootstepRules.LevelDb(FootSurface.Concrete, Gait.CrouchWalk, 0f));
            var prone = FootstepRules.AudibleRange(FootstepRules.LevelDb(FootSurface.Concrete, Gait.Prone, 0f));
            Assert.AreEqual(14f, walk, 2f);
            Assert.AreEqual(40f, sprint, 4f);
            Assert.AreEqual(4.5f, crouch, 1f);
            Assert.Less(prone, 3f);
        }

        [Test]
        public void AudibleRange_MetalCarriesFurtherThanSnow_AndNoiseMasks()
        {
            var metal = FootstepRules.AudibleRange(FootstepRules.LevelDb(FootSurface.Metal, Gait.Jog, 0f));
            var snow = FootstepRules.AudibleRange(FootstepRules.LevelDb(FootSurface.Snow, Gait.Jog, 0f));
            Assert.Greater(metal, snow * 2f);
            var lv = FootstepRules.LevelDb(FootSurface.Concrete, Gait.Walk, 0f);
            Assert.Less(FootstepRules.AudibleRange(lv, 55f), FootstepRules.AudibleRange(lv, 35f));
            Assert.AreEqual(0f, FootstepRules.AudibleRange(40f), 0.001f);
        }

        [Test]
        public void SourceVolume_ReferencesFullAt80()
        {
            Assert.AreEqual(1f, FootstepRules.SourceVolume(80f), 0.001f);
            Assert.AreEqual(0.316f, FootstepRules.SourceVolume(70f), 0.01f);
            Assert.AreEqual(1f, FootstepRules.SourceVolume(95f), 0.001f);
        }

        [Test]
        public void SurfaceKind_MapsToFootSurface()
        {
            Assert.AreEqual(FootSurface.Metal, FootstepRules.FromSurfaceKind(SurfaceKind.Metal));
            Assert.AreEqual(FootSurface.Foliage, FootstepRules.FromSurfaceKind(SurfaceKind.Foliage));
            Assert.AreEqual(FootSurface.Concrete, FootstepRules.FromSurfaceKind(SurfaceKind.Default));
        }

        [Test]
        public void Plan_SprintIsForefootAndFeetDiffer()
        {
            var walk = FootstepRules.Plan(FootSurface.Wood, Gait.Walk, 2f, 15f, 0f, true);
            var sprint = FootstepRules.Plan(FootSurface.Wood, Gait.Sprint, 5.5f, 15f, 0f, true);
            var right = FootstepRules.Plan(FootSurface.Wood, Gait.Walk, 2f, 15f, 0f, false);
            Assert.Less(sprint.ToeGain, walk.ToeGain);
            Assert.Less(sprint.ToeDelaySeconds, walk.ToeDelaySeconds);
            Assert.Less(walk.Pitch, right.Pitch);
            Assert.AreEqual(1f, FootstepRules.Plan(FootSurface.Water, Gait.Walk, 2f, 0f, 0f, true).SplashGain, 0.001f);
        }

        [Test]
        public void Audibility_DistanceBarrierAndMasking()
        {
            var lvl = FootstepRules.LevelDb(FootSurface.Concrete, Gait.Sprint, 0f);
            var near = FootstepAudibility.Evaluate(lvl, 10f, BarrierMaterial.None, 0f, 0f, 0.2f, 35f, 0f, 6500f);
            var far = FootstepAudibility.Evaluate(lvl, 80f, BarrierMaterial.None, 0f, 0f, 0.2f, 35f, 0f, 6500f);
            var walled = FootstepAudibility.Evaluate(lvl, 10f, BarrierMaterial.Concrete, 0f, 0f, 0.2f, 35f, 0f, 6500f);
            var masked = FootstepAudibility.Evaluate(lvl, 10f, BarrierMaterial.None, 0f, 0f, 0.2f, 35f, 95f, 6500f);
            Assert.IsTrue(near.Audible);
            Assert.IsFalse(far.Audible);
            Assert.IsFalse(walled.Audible);
            Assert.IsFalse(masked.Audible);
            Assert.Greater(near.Volume, 0f);
            Assert.AreEqual(0f, far.Volume, 0.0001f);
        }

        [Test]
        public void Audibility_WoodFloorCarriesBetterThanConcrete_AndBlursDirection()
        {
            var wood = FootstepAudibility.Evaluate(70f, 5f, BarrierMaterial.None, 0f, 3f, 1f, 35f, 0f, 5000f);
            var conc = FootstepAudibility.Evaluate(70f, 5f, BarrierMaterial.None, 0f, 3f, 0.05f, 35f, 0f, 5000f);
            var same = FootstepAudibility.Evaluate(70f, 5f, BarrierMaterial.None, 0f, 0f, 1f, 35f, 0f, 5000f);
            Assert.Greater(wood.LevelAtListenerDb, conc.LevelAtListenerDb + 20f);
            Assert.Greater(wood.SpreadDeg, same.SpreadDeg);
            Assert.Less(wood.CutoffHz, 1000f);
        }

        [Test]
        public void AirCutoff_DropsWithDistance()
        {
            Assert.AreEqual(20000f, FootstepAudibility.AirCutoffHz(15f), 0.1f);
            Assert.AreEqual(2000f, FootstepAudibility.AirCutoffHz(90f), 5f);
            Assert.Less(FootstepAudibility.AirCutoffHz(50f), FootstepAudibility.AirCutoffHz(20f));
        }

        [Test]
        public void GunMasking_FadesOverTime()
        {
            Assert.AreEqual(80f, FootstepAudibility.GunMaskingDb(100f, 0f), 0.01f);
            Assert.AreEqual(40f, FootstepAudibility.GunMaskingDb(100f, 0.4f), 0.01f);
            Assert.AreEqual(0f, FootstepAudibility.GunMaskingDb(100f, 0.9f), 0.01f);
        }

        [Test]
        public void Scheduler_AlternatesFeet_AndMatchesStride()
        {
            var s = new FootfallScheduler();
            var list = new List<FootfallEvent>();
            for (var i = 0; i < 100; i++)
                if (s.Tick(2f, false, false, true, 0.1f, out var ev)) list.Add(ev);
            Assert.IsTrue(list.Count >= 25 && list.Count <= 27);
            for (var i = 1; i < list.Count; i++)
                Assert.IsTrue(list[i].LeftFoot != list[i - 1].LeftFoot);
            Assert.AreEqual(Gait.Walk, list[0].Gait);
        }

        [Test]
        public void Scheduler_NoStepsWhenAirborneOrStanding()
        {
            var s = new FootfallScheduler();
            for (var i = 0; i < 50; i++)
            {
                Assert.IsFalse(s.Tick(5f, false, false, false, 0.1f, out var a));
                Assert.IsFalse(s.Tick(0.1f, false, false, true, 0.1f, out var b));
            }
            Assert.IsTrue(float.IsPositiveInfinity(s.SecondsToNext(0f, false, false)));
        }

        [Test]
        public void VariantPicker_NeverRepeatsRecent()
        {
            var p = new FootstepVariantPicker(12345u);
            var a = -1;
            var b = -1;
            for (var i = 0; i < 300; i++)
            {
                var n = p.Pick(4);
                Assert.IsTrue(n >= 0 && n < 4);
                Assert.IsTrue(n != a && n != b);
                b = a;
                a = n;
            }
            Assert.AreEqual(0, p.Pick(1));
        }
    }
}
