#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Foley;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class LoudnessMathTests
    {
        private static float[] Sine(float amp, float hz = 1000f, int n = 44100)
        {
            var s = new float[n];
            for (var i = 0; i < n; i++)
                s[i] = amp * (float)Math.Sin(2 * Math.PI * hz * i / 44100.0);
            return s;
        }

        [Test]
        public void Peak_Db_And_Ceiling()
        {
            Assert.AreEqual(0f, LoudnessMath.PeakDb(Sine(1f)), 0.05f);
            Assert.AreEqual(-6.02f, LoudnessMath.PeakDb(Sine(0.5f)), 0.1f);
            Assert.AreEqual(0.891f, LoudnessMath.PeakCeilingLinear, 0.002f);
            Assert.AreEqual(LoudnessMath.SilenceDb, LoudnessMath.PeakDb(new float[10]));
        }

        [Test]
        public void ApproxLufs_DoublesWithAmplitude()
        {
            var a = LoudnessMath.ApproxLufs(Sine(0.25f));
            var b = LoudnessMath.ApproxLufs(Sine(0.5f));
            Assert.AreEqual(6.02f, b - a, 0.2f);
            Assert.AreEqual(LoudnessMath.SilenceDb, LoudnessMath.ApproxLufs(new float[100]));
        }

        [Test]
        public void GainToTarget_RespectsPeakCeiling()
        {
            var g = LoudnessMath.GainToTarget(-30f, 0.8f, -20f);
            Assert.LessOrEqual(g * 0.8f, LoudnessMath.PeakCeilingLinear + 1e-4f);
            var g2 = LoudnessMath.GainToTarget(-30f, 0.05f, -24f);
            Assert.AreEqual(Math.Pow(10, 6 / 20.0), g2, 0.01);
            Assert.AreEqual(1f, LoudnessMath.GainToTarget(-200f, 0.5f, -20f));
        }

        [Test]
        public void Targets_SuppressedQuietest_SniperLoudest()
        {
            Assert.Less(LoudnessMath.TargetLufs(LoudnessClass.Suppressed), LoudnessMath.TargetLufs(LoudnessClass.Pistol));
            Assert.Greater(LoudnessMath.TargetLufs(LoudnessClass.Sniper), LoudnessMath.TargetLufs(LoudnessClass.Rifle));
            Assert.AreEqual(LoudnessClass.Suppressed, LoudnessMath.ClassOf(WeaponCategory.Sniper, true));
            Assert.AreEqual(LoudnessClass.Lmg, LoudnessMath.ClassOf(WeaponCategory.Lmg, false));
        }

        [Test]
        public void InteriorTail_AutoSelection()
        {
            Assert.AreEqual(FireLayer.TailIndoor, LoudnessMath.TailLayerFor(AudioEnvironment.Indoor));
            Assert.AreEqual(FireLayer.TailValley, LoudnessMath.TailLayerFor(AudioEnvironment.Valley));
            Assert.AreEqual(FireLayer.TailOutdoor, LoudnessMath.TailLayerFor(AudioEnvironment.Outdoor));
            Assert.AreEqual(SoundId.ShotTailIndoor, LoudnessMath.TailSoundFor(AudioEnvironment.Indoor));
            Assert.AreEqual(AudioLayers.TailFor(AudioEnvironment.Indoor), LoudnessMath.TailSoundFor(AudioEnvironment.Indoor));
            // Tavan çarpması iç mekân sınıflar.
            Assert.AreEqual(AudioEnvironment.Indoor, AudioLayers.Classify(true, 0));
        }

        [Test]
        public void FireLayerNames_IncludeBelt()
        {
            Assert.AreEqual("belt", FoleyNaming.FireLayerName(FireLayer.Belt));
            Assert.AreEqual("supp", FoleyNaming.FireLayerName(FireLayer.Suppressed));
            Assert.AreEqual("belt", FoleyNaming.LayerOfClipName("belt_2"));
        }

        [Test]
        public void SurfaceImpact_MapsAllSurfaces()
        {
            foreach (SurfaceKind k in Enum.GetValues(typeof(SurfaceKind)))
            {
                Assert.AreNotEqual(SoundId.None, SurfaceImpactAudio.SoundFor(k), k.ToString());
                Assert.IsTrue(SurfaceImpactAudio.FolderFor(k).StartsWith("Audio/SFX/Impact/"));
                Assert.Greater(SurfaceImpactAudio.VolumeScale(k), 0f);
            }

            Assert.AreEqual(SoundId.BulletImpactMetal, SurfaceImpactAudio.SoundFor(SurfaceKind.Metal));
            Assert.AreEqual(SoundId.BulletImpact, SurfaceImpactAudio.SoundFor(SurfaceKind.Concrete));
        }

        [Test]
        public void ProceduralFireLayers_UnderCeiling_AndRenderable()
        {
            var ids = new[]
            {
                SoundId.ShotPistol, SoundId.ShotSmg, SoundId.ShotRifle556, SoundId.ShotRifle762, SoundId.ShotDmr,
                SoundId.ShotSniper, SoundId.ShotShotgun, SoundId.ShotMachineGun, SoundId.ShotMech, SoundId.ShotThump,
                SoundId.ShotTailOutdoor, SoundId.ShotTailIndoor, SoundId.ShotTailValley, SoundId.BulletCrack,
                SoundId.BulletWhiz, SoundId.ShotSuppressed, SoundId.MgBeltRattle,
                SoundId.BulletImpactDirt, SoundId.BulletImpactWood, SoundId.BulletImpactFlesh,
                SoundId.BulletImpactWater, SoundId.BulletImpactSnow, SoundId.BulletImpactFoliage
            };
            foreach (var id in ids)
            {
                var s = ProceduralAudioSynth.Render(id);
                Assert.IsNotNull(s, id.ToString());
                for (var i = 0; i < s.Length; i++)
                    Assert.IsFalse(float.IsNaN(s[i]), id + " NaN");
                Assert.LessOrEqual(LoudnessMath.PeakDb(s), LoudnessMath.PeakCeilingDb + 0.05f, id + " tepe");
            }
        }

        [Test]
        public void Suppressed_QuieterThanRifle_ByClassTargets()
        {
            var rifle = LoudnessMath.ApproxLufs(ProceduralAudioSynth.Render(SoundId.ShotRifle556));
            var supp = LoudnessMath.ApproxLufs(ProceduralAudioSynth.Render(SoundId.ShotSuppressed));
            Assert.Less(supp, rifle);
        }
    }
}
#endif
