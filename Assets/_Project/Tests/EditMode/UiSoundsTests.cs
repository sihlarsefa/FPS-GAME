using NUnit.Framework;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Music;

namespace Project.Tests
{
    public sealed class UiSoundsTests
    {
        [Test]
        public void Envelope_RisesThenDecays()
        {
            Assert.That(UiSfxSynth.Envelope(0f, 0.01f, 0.05f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(UiSfxSynth.Envelope(0.01f, 0.01f, 0.05f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(UiSfxSynth.Envelope(0.06f, 0.01f, 0.05f), Is.EqualTo(0.3679f).Within(1e-3f));
            Assert.That(UiSfxSynth.Envelope(-1f, 0.01f, 0.05f), Is.EqualTo(0f));
        }

        [Test]
        public void NormalizeGain_RespectsPeakCeiling()
        {
            var g = UiSfxSynth.NormalizeGain(-40f, 0.9f, -18f, -3f);
            Assert.That(g * 0.9f, Is.LessThanOrEqualTo(LoudnessMath.FromDb(-3f) + 1e-4f));
            Assert.That(UiSfxSynth.NormalizeGain(-120f, 0f, -18f, -3f), Is.EqualTo(1f));
        }

        [Test]
        public void AllSfx_AreFinite_AndUnderCeiling()
        {
            foreach (UiSfx k in System.Enum.GetValues(typeof(UiSfx)))
            {
                var s = UiSfxSynth.Render(k);
                Assert.That(s.Length, Is.GreaterThan(100), k.ToString());
                var peak = LoudnessMath.PeakLinear(s);
                Assert.That(peak, Is.GreaterThan(0.01f), k.ToString());
                Assert.That(peak, Is.LessThanOrEqualTo(LoudnessMath.FromDb(-3f) + 1e-3f), k.ToString());
                foreach (var v in s) Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
            }
        }

        [Test]
        public void DuckGain_Bounds()
        {
            Assert.That(MusicMixMath.DuckGain(0f, 0.35f), Is.EqualTo(1f));
            Assert.That(MusicMixMath.DuckGain(1f, 0.35f), Is.EqualTo(0.65f).Within(1e-5f));
            Assert.That(MusicMixMath.DuckGain(5f, 2f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void FollowEnvelope_AttackFasterThanRelease()
        {
            var x = new float[22050];
            for (var i = 0; i < 2000; i++) x[i] = 1f;
            var e = MusicMixMath.FollowEnvelope(x, 22050, 0.01f, 0.5f, false);
            Assert.That(e[220], Is.GreaterThan(0.5f));
            Assert.That(e[2100], Is.GreaterThan(0.7f));
            Assert.That(e[22000], Is.LessThan(e[2100]));
        }

        [Test]
        public void SoftLimit_NeverExceedsCeiling()
        {
            var c = MusicMixMath.CeilingLinear;
            foreach (var v in new[] { 0.1f, 0.8f, 1f, 3f, 100f, -50f })
                Assert.That(System.Math.Abs(MusicMixMath.SoftLimit(v, c)), Is.LessThanOrEqualTo(c + 1e-6f));
            Assert.That(MusicMixMath.SoftLimit(0.1f, c), Is.EqualTo(0.1f));
        }
    }
}
