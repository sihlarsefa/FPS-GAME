#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class AudioLayersTests
    {
        [Test]
        public void LayerWeights_Crossfade()
        {
            AudioLayers.LayerWeights(5f, out var n, out var m, out var f);
            Assert.AreEqual(1f, n, 0.001f);
            Assert.AreEqual(0f, m, 0.001f);
            Assert.AreEqual(0f, f, 0.001f);
            AudioLayers.LayerWeights(150f, out n, out m, out f);
            Assert.AreEqual(0f, n, 0.001f);
            Assert.AreEqual(1f, m, 0.001f);
            AudioLayers.LayerWeights(500f, out n, out m, out f);
            Assert.AreEqual(0f, m, 0.001f);
            Assert.AreEqual(1f, f, 0.001f);
            AudioLayers.LayerWeights(55f, out n, out m, out f);
            Assert.IsTrue(n > 0f && n < 1f && m > 0f);
        }

        [Test]
        public void Occlusion_Monotonic()
        {
            Assert.AreEqual(1f, AudioLayers.OcclusionVolume(0), 0.001f);
            Assert.IsTrue(AudioLayers.OcclusionVolume(2) < AudioLayers.OcclusionVolume(1));
            Assert.IsTrue(AudioLayers.OcclusionCutoff(2) < AudioLayers.OcclusionCutoff(1));
            Assert.IsTrue(AudioLayers.OcclusionVolume(10) >= 0.3f);
        }

        [Test]
        public void Environment_Classification()
        {
            Assert.AreEqual(AudioEnvironment.Indoor, AudioLayers.Classify(true, 0));
            Assert.AreEqual(AudioEnvironment.Valley, AudioLayers.Classify(false, 3));
            Assert.AreEqual(AudioEnvironment.Outdoor, AudioLayers.Classify(false, 1));
            Assert.AreEqual(SoundId.ShotTailIndoor, AudioLayers.TailFor(AudioEnvironment.Indoor));
            Assert.AreEqual(SoundId.ShellDropMetal, AudioLayers.ShellDropFor(SurfaceKind.Metal));
        }

        [Test]
        public void RayBudget_LimitsPerFrame()
        {
            var b = new AudioLayers.RayBudget();
            var ok = 0;
            for (var i = 0; i < 20; i++)
                if (b.TryConsume(1)) ok++;
            Assert.AreEqual(AudioLayers.RaysPerFrame, ok);
            Assert.IsTrue(b.TryConsume(2));
        }

        [Test]
        public void VoiceCap_And_Priority()
        {
            Assert.IsFalse(AudioLayers.NeedsSteal(47));
            Assert.IsTrue(AudioLayers.NeedsSteal(48));
            Assert.IsTrue(AudioLayers.PriorityWeight(SoundId.Explosion) > AudioLayers.PriorityWeight(SoundId.ShellCasing));
        }

        [Test]
        public void SoundIds_AppendedAtEnd()
        {
            Assert.IsTrue((int)SoundId.ShotMech > (int)SoundId.ReloadSniper);
            Assert.IsTrue((int)SoundId.ShellDropMetal > (int)SoundId.ShotMech);
        }
    }
}
#endif
