#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class AudioQualityTests
    {
        [Test]
        public void ShotTier_ByDistance()
        {
            Assert.AreEqual(AudioQuality.ShotTier.Close, AudioQuality.TierForDistance(10f));
            Assert.AreEqual(AudioQuality.ShotTier.Mid, AudioQuality.TierForDistance(100f));
            Assert.AreEqual(AudioQuality.ShotTier.Far, AudioQuality.TierForDistance(400f));
        }

        [Test]
        public void Footstep_PerSurface()
        {
            Assert.AreEqual(SoundId.FootstepMetal, AudioQuality.FootstepFor(SurfaceKind.Metal));
            Assert.AreEqual(SoundId.FootstepWood, AudioQuality.FootstepFor(SurfaceKind.Wood));
            Assert.AreEqual(SoundId.FootstepConcrete, AudioQuality.FootstepFor(SurfaceKind.Concrete));
            Assert.AreEqual(SoundId.FootstepGrass, AudioQuality.FootstepFor(SurfaceKind.Foliage));
            Assert.AreEqual(SoundId.Footstep, AudioQuality.FootstepFor(SurfaceKind.Dirt));
        }

        [Test]
        public void Reload_PerWeaponClass()
        {
            Assert.AreEqual(SoundId.ReloadPistol, AudioQuality.ReloadFor(WeaponCategory.Pistol));
            Assert.AreEqual(SoundId.ReloadLmg, AudioQuality.ReloadFor(WeaponCategory.Lmg));
            Assert.AreEqual(SoundId.None, AudioQuality.ReloadFor(WeaponCategory.Melee));
        }

        [Test]
        public void TerrainLayerNames_Map()
        {
            Assert.AreEqual(SurfaceKind.Foliage, AudioQuality.KindFromLayerName("Grass_01"));
            Assert.AreEqual(SurfaceKind.Concrete, AudioQuality.KindFromLayerName("RockCliff"));
            Assert.AreEqual(SurfaceKind.Dirt, AudioQuality.KindFromLayerName("Mud"));
        }

        [Test]
        public void NewSounds_RenderUnderBudget()
        {
            // Yalnızca üretim süresi ölçülür; NaN taraması (örnek başına dizge birleştirmesi/Assert çağrısı yapmadan) süre dışında.
            var sw = new System.Diagnostics.Stopwatch();
            for (var id = SoundId.ShotDistantMid; id <= SoundId.ReloadSniper; id++)
            {
                sw.Start();
                var s = ProceduralAudioSynth.Render(id);
                sw.Stop();
                Assert.IsNotNull(s, id.ToString());
                Assert.Greater(s.Length, 100, id.ToString());
                var nan = false;
                for (var i = 0; i < s.Length; i++)
                    if (float.IsNaN(s[i])) { nan = true; break; }
                Assert.IsFalse(nan, id + " NaN");
            }

            Assert.Less(sw.ElapsedMilliseconds, 400, "toplam üretim süresi");
        }
    
        [Test]
        public void StepVariants_AtLeastFour_AllSurfaces_Distinct()
        {
            foreach (SurfaceKind k in System.Enum.GetValues(typeof(SurfaceKind)))
            {
                Assert.GreaterOrEqual(AudioQuality.StepVariantCount(k), 4);
                var seen = new System.Collections.Generic.HashSet<string>();
                for (var v = 0; v < AudioQuality.StepVariantCount(k); v++)
                {
                    var r = AudioQuality.StepVariant(k, v);
                    Assert.AreNotEqual(SoundId.None, r.Id);
                    seen.Add(r.Id + "/" + r.Pitch + "/" + r.Extra);
                }
                Assert.GreaterOrEqual(seen.Count, 4, k.ToString());
            }
        }

        [Test]
        public void StepVariant_SurfaceCharacter()
        {
            Assert.AreEqual(SoundId.FootstepSnow, AudioQuality.StepVariant(SurfaceKind.Snow, 0).Id);
            Assert.AreEqual(SoundId.WoodCreak, AudioQuality.StepVariant(SurfaceKind.Wood, 1).Extra);
            Assert.AreEqual(SoundId.FootstepMud, AudioQuality.StepVariant(SurfaceKind.Dirt, 1).Id);
            Assert.AreEqual(SoundId.FootstepGravel, AudioQuality.StepVariant(SurfaceKind.Dirt, 2).Id);
        }

        [Test]
        public void NextStepVariant_NeverRepeats_AndInRange()
        {
            for (var last = -1; last < 5; last++)
                for (var i = 0; i <= 20; i++)
                {
                    var n = AudioQuality.NextStepVariant(SurfaceKind.Wood, last, i / 20.001f);
                    Assert.That(n, Is.InRange(0, 4));
                    if (last >= 0) Assert.AreNotEqual(last, n);
                }
        }

        [Test]
        public void Gait_WalkSoftSprintHard_CrouchQuiet()
        {
            Assert.Less(AudioQuality.GaitVolume(Stance.Standing, false), AudioQuality.GaitVolume(Stance.Standing, true));
            Assert.Less(AudioQuality.GaitVolume(Stance.Crouching, false), AudioQuality.GaitVolume(Stance.Standing, false));
            Assert.IsTrue(AudioQuality.GearJingleOnStep(Stance.Standing, true, 2));
            Assert.IsFalse(AudioQuality.GearJingleOnStep(Stance.Standing, false, 2));
            Assert.IsFalse(AudioQuality.GearJingleOnStep(Stance.Crouching, true, 2));
        }

        [Test]
        public void ActorScale_BotIs15PercentQuieter_StillAudible()
        {
            Assert.AreEqual(0.85f, AudioQuality.ActorStepScale(false, true), 1e-4f);
            Assert.Less(AudioQuality.ActorStepScale(true, false), 1f);
            Assert.AreEqual(1f, AudioQuality.ActorStepScale(false, false), 1e-4f);
            Assert.AreEqual(AudioQuality.MinAudibleStepVolume, AudioQuality.FinalStepVolume(0.01f, false, true), 1e-4f);
            Assert.AreEqual(0.01f, AudioQuality.FinalStepVolume(0.01f, true, false), 1e-4f);
        }

        [Test]
        public void LandingTweak_PerSurface()
        {
            AudioQuality.LandingTweak(SurfaceKind.Metal, out var vm, out _, out var lm);
            AudioQuality.LandingTweak(SurfaceKind.Snow, out var vs, out _, out var ls);
            Assert.Greater(vm, vs);
            Assert.AreEqual(SoundId.FootstepMetal, lm);
            Assert.AreEqual(SoundId.FootstepSnow, ls);
        }
}
}
#endif
