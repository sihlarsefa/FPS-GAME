using NUnit.Framework;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Ui;

namespace Project.Tests.EditMode
{
    public sealed class UiAudioRulesTests
    {
        [Test]
        public void Hierarchy_StingerLoudest_BedQuieterThanConfirm()
        {
            Assert.Greater(UiAudioRules.LayerDb(UiLayer.Stinger), UiAudioRules.LayerDb(UiLayer.UiConfirm));
            Assert.Greater(UiAudioRules.LayerDb(UiLayer.UiConfirm), UiAudioRules.LayerDb(UiLayer.Bed));
            Assert.Less(UiAudioRules.LayerDb(UiLayer.UiNav), UiAudioRules.LayerDb(UiLayer.UiConfirm));
            Assert.AreEqual(0.3981f, UiAudioRules.LayerGain(UiLayer.Stinger), 0.002f);
        }

        [Test]
        public void Duck_HoverNone_StingerDeepest()
        {
            Assert.AreEqual(0f, UiAudioRules.DuckDepthDb(UiSfx.Hover), 0.0001f);
            Assert.Less(UiAudioRules.DuckDepthDb(UiSfx.MatchFound), UiAudioRules.DuckDepthDb(UiSfx.Confirm));
            Assert.AreEqual(-9f, UiAudioRules.DeeperDuck(-9f, -3f), 0.0001f);
            Assert.AreEqual(-3f, UiAudioRules.DeeperDuck(0f, -3f), 0.0001f);
        }

        [Test]
        public void DuckFollower_AttacksFastReleasesSlow()
        {
            var d = new DuckFollower(0.06f, 0.8f);
            for (var i = 0; i < 10; i++) d.Step(0.016f, -9f);
            Assert.Less(d.CurrentDb, -5f);
            var deep = d.CurrentDb;
            d.Step(0.1f, 0f);
            Assert.Greater(d.CurrentDb, deep);
            Assert.Less(d.CurrentDb, -3f);
            for (var i = 0; i < 400; i++) d.Step(0.016f, 0f);
            Assert.AreEqual(0f, d.CurrentDb, 0.02f);
            Assert.AreEqual(1f, d.Gain, 0.01f);
        }

        [Test]
        public void DuckFollower_IgnoresNaNAndPositiveTargets()
        {
            var d = new DuckFollower();
            d.Step(0.05f, float.NaN);
            d.Step(0.05f, 6f);
            Assert.AreEqual(0f, d.CurrentDb, 0.0001f);
        }

        [Test]
        public void Variation_NeverRepeatsClosely_StaysInRange()
        {
            var prev = 0.3f;
            for (var i = 0; i <= 40; i++)
            {
                var v = UiAudioRules.NextVariation(prev, i / 40f, 0.7f, 0.25f);
                Assert.IsTrue(System.Math.Abs(v - prev) >= 0.249f, "adim " + i);
                Assert.IsTrue(System.Math.Abs(v) <= 0.7001f, "aralik " + i);
                prev = v;
            }
        }

        [Test]
        public void HoverLadder_ClimbsThenResets()
        {
            var c = 0;
            for (var i = 0; i < 10; i++) c = UiAudioRules.NextHoverChain(c, 0.2f);
            Assert.AreEqual(6, c);
            Assert.AreEqual(3f, UiAudioRules.HoverSemitone(c), 0.0001f);
            Assert.AreEqual(0, UiAudioRules.NextHoverChain(c, 1.5f));
            Assert.AreEqual(2f, UiAudioRules.SemitoneRatio(12f), 0.0001f);
        }

        [Test]
        public void UiPitchState_HoverPitchRisesInChain()
        {
            var st = new UiPitchState(7u);
            var first = st.HoverPitch(10f);
            st.HoverPitch(10.2f);
            st.HoverPitch(10.4f);
            st.HoverPitch(10.6f);
            var fifth = st.HoverPitch(10.8f);
            Assert.Greater(fifth, first);
            Assert.Less(st.HoverPitch(30f), fifth);
        }

        [Test]
        public void Distance_CutoffFallsAndLossGrows()
        {
            Assert.AreEqual(1800f, UiAudioRules.DistanceCutoffHz(1.5f), 1f);
            Assert.AreEqual(300f, UiAudioRules.DistanceCutoffHz(6f), 2f);
            Assert.Less(UiAudioRules.DistanceCutoffHz(4f), UiAudioRules.DistanceCutoffHz(2f));
            Assert.AreEqual(-10f, UiAudioRules.DistanceLossDb(4f), 0.001f);
            Assert.AreEqual(0f, UiAudioRules.DistanceLossDb(-3f), 0.001f);
        }

        [Test]
        public void Artillery_DelayClampedAndSalvoCounts()
        {
            Assert.AreEqual(18f, UiAudioRules.NextArtilleryDelay(0.001f), 0.001f);
            Assert.AreEqual(90f, UiAudioRules.NextArtilleryDelay(0.999f), 0.001f);
            Assert.Greater(UiAudioRules.NextArtilleryDelay(0.7f), UiAudioRules.NextArtilleryDelay(0.5f));
            Assert.AreEqual(1, UiAudioRules.SalvoCount(0.2f));
            Assert.AreEqual(2, UiAudioRules.SalvoCount(0.6f));
            Assert.AreEqual(3, UiAudioRules.SalvoCount(0.95f));
            Assert.AreEqual(0.7f, UiAudioRules.SalvoGap(0f), 0.001f);
            Assert.AreEqual(2.2f, UiAudioRules.SalvoGap(1f), 0.001f);
            Assert.IsFalse(UiAudioRules.ArtilleryAllowed(10f, 8f));
            Assert.IsTrue(UiAudioRules.ArtilleryAllowed(13f, 8f));
        }

        [Test]
        public void ArtillerySynth_FiniteBoundedAndLonger_WhenFarther()
        {
            var near = DistantArtillerySynth.Render(2f, 11u);
            var far = DistantArtillerySynth.Render(6f, 11u);
            Assert.Greater(far.Length, near.Length);
            Assert.AreEqual(0.8f, LoudnessMath.PeakLinear(near), 0.01f);
            for (var i = 0; i < far.Length; i += 97)
                Assert.IsTrue(!float.IsNaN(far[i]) && System.Math.Abs(far[i]) <= 1f);
            // Uzak olan daha az tiz: ilk 0.1 sn'de yakın olandan daha az ortalama mutlak fark.
            float dn = 0f, df = 0f;
            for (var i = 1; i < 2200; i++) { dn += System.Math.Abs(near[i] - near[i - 1]); df += System.Math.Abs(far[i] - far[i - 1]); }
            Assert.Less(df, dn);
        }

        [Test]
        public void ExtraSfx_RenderedNormalizedAndTonal()
        {
            var kinds = new[] { UiSfx.Select, UiSfx.Confirm, UiSfx.Cancel, UiSfx.ToggleOn, UiSfx.ToggleOff };
            foreach (var k in kinds)
            {
                Assert.IsTrue(UiSfxExtraSynth.Handles(k));
                var s = UiSfxSynth.Render(k);
                Assert.Greater(s.Length, 500, k.ToString());
                Assert.IsTrue(LoudnessMath.PeakLinear(s) <= 0.71f, k.ToString());
                Assert.Greater(LoudnessMath.PeakLinear(s), 0.05f, k.ToString());
            }
            Assert.IsFalse(UiSfxExtraSynth.Handles(UiSfx.Hover));
        }
    }
}
