using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Tests.EditMode.Sim;

namespace Project.Tests.EditMode
{
    public sealed class SmallFeelSettingsTests
    {
        [Test]
        public void Defaults_AreBackwardCompatible()
        {
            var s = new SettingsService(null).Current;
            Assert.AreEqual(54f, s.ViewmodelFov);
            Assert.IsTrue(s.AdsFovRelativeSensitivity);
            Assert.IsTrue(s.VolumetricFog);
            Assert.IsTrue(s.ContactShadows);
            Assert.IsTrue(s.ScreenSpaceReflections);
        }

        [Test]
        public void ViewmodelFov_ClampsTo50_80()
        {
            Assert.AreEqual(50f, SettingsService.Sanitize(new GameSettings { ViewmodelFov = 10f }).ViewmodelFov);
            Assert.AreEqual(80f, SettingsService.Sanitize(new GameSettings { ViewmodelFov = 200f }).ViewmodelFov);
        }

        [Test]
        public void NewFields_RoundTripThroughStore()
        {
            var store = new SimMemoryStore();
            new SettingsService(store).Modify(s => { s.ViewmodelFov = 72f; s.AdsFovRelativeSensitivity = false; s.ContactShadows = false; });
            var reader = new SettingsService(store);
            reader.Load();
            Assert.AreEqual(72f, reader.Current.ViewmodelFov);
            Assert.IsFalse(reader.Current.AdsFovRelativeSensitivity);
            Assert.IsFalse(reader.Current.ContactShadows);
            Assert.IsTrue(reader.Current.ScreenSpaceReflections);
        }

        [Test]
        public void CameraKick_ScalesWithIntensityAndAds()
        {
            Assert.AreEqual(0f, CameraKickMath.Scale(0f, false));
            Assert.IsTrue(CameraKickMath.Scale(1f, true) < CameraKickMath.Scale(1f, false));
            Assert.AreEqual(CameraKickMath.Scale(1.5f, false), CameraKickMath.Scale(9f, false));
            CameraKickMath.Shot(1f, false, 2f, 1f, out var p, out var y);
            Assert.AreEqual(CameraKickMath.BasePitchPerShot, p, 1e-5f);
            Assert.AreEqual(CameraKickMath.BaseYawPerShot, y, 1e-5f);
        }
    }
}
