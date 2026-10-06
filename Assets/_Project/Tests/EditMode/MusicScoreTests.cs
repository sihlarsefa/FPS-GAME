using System.Linq;
using NUnit.Framework;
using Project.Infrastructure.Audio.Music;

namespace Project.Tests
{
    public sealed class MusicScoreTests
    {
        [Test]
        public void LoopIs75Seconds()
        {
            Assert.That(MusicScore.LoopSeconds, Is.EqualTo(75f).Within(0.001f));
            Assert.That(MusicScore.LoopSeconds, Is.InRange(60f, 90f));
        }

        [Test]
        public void MidiToHz_A4Is440()
        {
            Assert.That(MusicScore.MidiToHz(69), Is.EqualTo(440f).Within(0.01f));
            Assert.That(MusicScore.MidiToHz(57), Is.EqualTo(220f).Within(0.01f));
        }

        [Test]
        public void ChordsTileTheLoopExactly()
        {
            var chords = MusicScore.BuildChords();
            var t = 0f;
            foreach (var c in chords) { Assert.That(c.StartBeat, Is.EqualTo(t)); t += c.Beats; }
            Assert.That(t, Is.EqualTo(MusicScore.LoopBeats));
        }

        [Test]
        public void MelodyStaysInScaleAndInsideLoop()
        {
            var m = MusicScore.BuildMelody();
            Assert.That(m.Count, Is.GreaterThan(10));
            foreach (var n in m)
            {
                Assert.That(MusicScore.InScale(n.Midi), Is.True, "midi " + n.Midi);
                Assert.That(n.StartBeat, Is.GreaterThanOrEqualTo(0f));
                Assert.That(n.StartBeat + n.Beats, Is.LessThanOrEqualTo(MusicScore.LoopBeats + 0.01f));
            }
            Assert.That(m.Last().Midi, Is.EqualTo(69), "melodi La'ya çözülmeli");
        }

        [Test]
        public void DrumsAreSparseSortedAndInLoop()
        {
            var d = MusicScore.BuildDrums();
            Assert.That(d.Count, Is.LessThan(MusicScore.LoopBars * 4));
            for (var i = 0; i < d.Count; i++)
            {
                Assert.That(d[i].Beat, Is.InRange(0f, MusicScore.LoopBeats));
                if (i > 0) Assert.That(d[i].Beat, Is.GreaterThanOrEqualTo(d[i - 1].Beat));
            }
        }

        [TestCase(StingKind.MatchStart, 20f)]
        [TestCase(StingKind.Victory, 9f)]
        [TestCase(StingKind.Defeat, 10f)]
        public void StingsHaveContentWithinDuration(StingKind kind, float seconds)
        {
            var s = MusicScore.BuildSting(kind);
            Assert.That(s.Seconds, Is.EqualTo(seconds));
            Assert.That(s.Drums.Count, Is.GreaterThan(0));
            foreach (var d in s.Drums) Assert.That(d.Beat, Is.LessThan(s.Seconds));
            foreach (var n in s.Melody) Assert.That(n.StartBeat, Is.LessThan(s.Seconds));
        }

        [Test]
        public void MatchStartHeartbeatAccelerates()
        {
            var s = MusicScore.BuildSting(StingKind.MatchStart);
            var main = s.Drums.Where(d => d.Velocity > 0.5f).Select(d => d.Beat).ToList();
            Assert.That(main[1] - main[0], Is.GreaterThan(main[main.Count - 2] - main[main.Count - 3]));
        }

        [Test]
        public void WavRoundTrip()
        {
            var b = new StereoBuffer { SampleRate = 22050, Left = new[] { 0f, 0.5f, -0.5f }, Right = new[] { 0.25f, 0f, 1f } };
            var r = MusicCache.ReadWav(MusicCache.WriteWav(b));
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Frames, Is.EqualTo(3));
            Assert.That(r.Left[1], Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(r.Right[0], Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void StingRenderIsFiniteAndAudible()
        {
            var b = MusicSynth.RenderSting(StingKind.Defeat);
            var peak = 0f;
            for (var i = 0; i < b.Frames; i++)
            {
                Assert.That(float.IsNaN(b.Left[i]), Is.False);
                peak = System.Math.Max(peak, System.Math.Abs(b.Left[i]));
            }
            Assert.That(peak, Is.InRange(0.3f, 1f));
        }
    }
}
