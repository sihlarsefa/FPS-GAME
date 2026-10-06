#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio.Weather;

namespace Project.Tests.EditMode
{
    public sealed class ThunderSynthTests
    {
        [Test]
        public void Cutoff_FallsWithDistance()
        {
            Assert.Greater(ThunderSynth.CutoffHz(300f), ThunderSynth.CutoffHz(1500f));
            Assert.Greater(ThunderSynth.CutoffHz(1500f), ThunderSynth.CutoffHz(3000f));
        }

        [Test]
        public void Crack_OnlyWhenNear()
        {
            Assert.Greater(ThunderSynth.CrackAmount(300f), 0.9f);
            Assert.AreEqual(0f, ThunderSynth.CrackAmount(2500f), 0.0001f);
        }

        [Test]
        public void Render_NonEmpty_PeakBounded_Deterministic()
        {
            var a = ThunderSynth.Render(500f, 3);
            var b = ThunderSynth.Render(500f, 3);
            Assert.Greater(a.Length, 44100);
            Assert.AreEqual(a.Length, b.Length);
            var peak = 0f;
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i], b[i], 0f);
                var v = a[i] < 0 ? -a[i] : a[i];
                if (v > peak) peak = v;
            }
            Assert.Greater(peak, 0.5f);
            Assert.Less(peak, 1.0f);
            Assert.Greater(ThunderSynth.Render(2800f, 3).Length, a.Length);
        }

        [Test]
        public void Flash_PulseCountAndDurationInRange()
        {
            for (var s = 0; s < 200; s++)
            {
                var n = LightningFlash.PulseCount(s);
                Assert.IsTrue(n >= 1 && n <= 3);
                var t = LightningFlash.TotalSeconds(s);
                Assert.IsTrue(t >= 0.0999f && t <= 0.3001f);
                Assert.AreEqual(0f, LightningFlash.Envelope(t + 0.01f, s), 0.0001f);
                Assert.AreEqual(0f, LightningFlash.Envelope(-0.1f, s), 0.0001f);
            }
        }
    }
}
#endif
