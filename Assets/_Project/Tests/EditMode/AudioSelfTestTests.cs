#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using Project.Infrastructure.Audio;
using Project.Presentation.Bootstrap;

namespace Project.Tests.EditMode
{
    public sealed class AudioSelfTestTests
    {
        [Test]
        public void Flag_ParsesAudioMode_WithDefaultTimeout()
        {
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "game", "-otoses", "/tmp/ses" }, out var o));
            Assert.AreEqual(OtoEkranMode.Ses, o.Mode);
            Assert.AreEqual("/tmp/ses", o.OutDir);
            Assert.AreEqual(90f, o.TimeoutSeconds);
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "-otoses", "/o", "-otoekran-sure", "200" }, out o));
            Assert.AreEqual(200f, o.TimeoutSeconds);
        }

        [Test]
        public void Flag_MissingDir_FailsAndIsReported()
        {
            Assert.IsFalse(OtoEkranArgs.TryParse(new[] { "-otoses" }, out _, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.IsTrue(OtoEkranArgs.HasAnyFlag(new[] { "-otoses" }));
        }

        [Test]
        public void Schedule_CoversAllStepsInOrder()
        {
            var s = AudioSelfTest.BuildSchedule();
            Assert.AreEqual(3, s.Count(e => e.Step == AudioSelfStep.OwnShot));
            Assert.AreEqual(1, s.Count(e => e.Step == AudioSelfStep.DistantShot));
            Assert.AreEqual(1, s.Count(e => e.Step == AudioSelfStep.ReloadStart));
            Assert.AreEqual(10, s.Count(e => e.Step == AudioSelfStep.Footstep));
            Assert.AreEqual(1, s.Count(e => e.Step == AudioSelfStep.MenuSting));
            var on = s.First(e => e.Step == AudioSelfStep.AmbienceOn).Time;
            var off = s.First(e => e.Step == AudioSelfStep.AmbienceOff).Time;
            Assert.AreEqual(5f, off - on, 0.001f);
            for (var i = 1; i < s.Count; i++)
                Assert.GreaterOrEqual(s[i].Time, s[i - 1].Time);
            Assert.AreEqual(AudioSelfTest.DurationSeconds, s[s.Count - 1].Time, 0.001f);
        }

        [Test]
        public void Row_HasSameColumnCountAsHeader_AndInvariantNumbers()
        {
            var row = AudioSelfTest.FormatRow(new AudioSelfSample
            {
                Time = 1.5f, Voices = 3, MixCutoffHz = 22000f, ListenerCutoffHz = 22000f, HdrTopDb = -12.34f, HdrGain = 0.5f,
                MasterVolume = 1f, AmbientVolume = 0.8f, UiVolume = 0.8f, MusicVolume = 0.7f, ListenerVolume = 1f,
                MixMasterGain = 1f, MixAmbienceGain = 1f, Event = "own_shot_1"
            });
            Assert.AreEqual(AudioSelfTest.Header.Split(',').Length, row.Split(',').Length);
            StringAssert.StartsWith("1.50,3,22000,22000,-12.3,0.5,", row);
        }

        [Test]
        public void IdleCheck_OpenFilter_ProducesExpectedLine()
        {
            Assert.AreEqual("[SES] boğukluk kontrol: cutoff=22000 idle", AudioSelfTest.IdleCheckLine(22000f, 22000f));
            StringAssert.Contains("BAŞARISIZ", AudioSelfTest.IdleCheckLine(22000f, 3200f));
            Assert.IsFalse(AudioSelfTest.IsOpen(650f, 22000f));
        }
    }
}
#endif
