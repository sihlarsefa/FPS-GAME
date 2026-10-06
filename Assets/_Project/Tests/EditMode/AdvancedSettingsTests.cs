using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Tests.EditMode.Sim;

namespace Project.Tests.EditMode
{
    public sealed class AdvancedSettingsTests
    {
        [Test]
        public void Defaults_AreSane()
        {
            var s = new SettingsService(null).Current;
            Assert.AreEqual(60, s.FrameRateCap);
            Assert.AreEqual(1f, s.RenderScale);
            Assert.IsFalse(s.ColorBlindMode);
            Assert.AreEqual(0, s.ResolutionWidth);
        }

        [Test]
        public void Sanitize_ClampsAndSnaps()
        {
            var s = SettingsService.Sanitize(new GameSettings
            {
                RenderScale = 5f, HudScale = 0.1f, CrosshairSize = 9f, CrosshairColor = 99, FrameRateCap = 100,
                WindowMode = 2, ResolutionWidth = 1920, ResolutionHeight = 0, SfxVolume = -1f
            });
            Assert.AreEqual(1f, s.RenderScale);
            Assert.AreEqual(0.8f, s.HudScale);
            Assert.AreEqual(2f, s.CrosshairSize);
            Assert.AreEqual(4, s.CrosshairColor);
            Assert.AreEqual(120, s.FrameRateCap);
            Assert.IsFalse(s.Fullscreen);
            Assert.AreEqual(0, s.ResolutionWidth);
            Assert.AreEqual(0f, s.SfxVolume);
        }

        [Test]
        public void RoundTripsThroughStore()
        {
            var store = new SimMemoryStore();
            new SettingsService(store).Modify(s =>
            {
                s.ResolutionWidth = 1280; s.ResolutionHeight = 720; s.WindowMode = 1; s.VSync = true; s.FrameRateCap = 0;
                s.RenderScale = 0.75f; s.VoiceVolume = 0.3f; s.ColorBlindMode = true; s.MotionBlur = true; s.CrosshairColor = 3;
            });
            var r = new SettingsService(store);
            r.Load();
            Assert.AreEqual(1280, r.Current.ResolutionWidth);
            Assert.AreEqual(1, r.Current.WindowMode);
            Assert.IsTrue(r.Current.VSync);
            Assert.AreEqual(0, r.Current.FrameRateCap);
            Assert.AreEqual(0.75f, r.Current.RenderScale, 1e-4f);
            Assert.AreEqual(0.3f, r.Current.VoiceVolume, 1e-4f);
            Assert.IsTrue(r.Current.ColorBlindMode);
            Assert.AreEqual(3, r.Current.CrosshairColor);
        }

        [Test]
        public void LegacyWindowedFlag_MapsToWindowMode()
        {
            var store = new SimMemoryStore();
            store.SetInt(SettingsService.Keys.Fullscreen, 0);
            var r = new SettingsService(store);
            r.Load();
            Assert.AreEqual(2, r.Current.WindowMode);
        }
    }
}
