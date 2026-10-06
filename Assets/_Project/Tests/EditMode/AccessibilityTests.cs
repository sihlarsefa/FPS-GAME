using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Tests.EditMode.Sim;

namespace Project.Tests.EditMode
{
    public sealed class AccessibilityTests
    {
        [Test]
        public void Settings_PersistAndClamp()
        {
            var store = new SimMemoryStore();
            new SettingsService(store).Modify(s =>
            {
                s.AimAssistStrength = 250; s.SubtitleSize = 9; s.SubtitleBackground = true;
                s.ToggleAds = true; s.ToggleCrouch = true; s.ColorBlindPalette = 2;
            });
            var r = new SettingsService(store);
            r.Load();
            Assert.AreEqual(100, r.Current.AimAssistStrength);
            Assert.AreEqual(3, r.Current.SubtitleSize);
            Assert.IsTrue(r.Current.SubtitleBackground);
            Assert.IsTrue(r.Current.ToggleAds);
            Assert.IsTrue(r.Current.ToggleCrouch);
            Assert.AreEqual(2, r.Current.ColorBlindPalette);
            Assert.IsTrue(r.Current.ColorBlindMode);
        }

        [Test]
        public void LegacyColorBlindFlag_MapsToPalette()
        {
            var s = new SettingsService(null);
            s.Modify(x => x.ColorBlindMode = true);
            Assert.AreEqual(1, s.Current.ColorBlindPalette);
        }

#if UNITY_EDITOR
        [Test]
        public void Slowdown_OnlyInsideCone()
        {
            Assert.AreEqual(1f, Project.Presentation.Player.AimAssistMath.SlowdownFactor(1f, 10f), 1e-5f);
            Assert.AreEqual(1f, Project.Presentation.Player.AimAssistMath.SlowdownFactor(0f, 0f), 1e-5f);
            Assert.Less(Project.Presentation.Player.AimAssistMath.SlowdownFactor(1f, 0f), 0.5f);
        }

        [Test]
        public void Magnet_NeverOvershoots_AndRespectsCone()
        {
            Assert.AreEqual(0f, Project.Presentation.Player.AimAssistMath.MagnetStep(10f, 1f, 0.016f));
            var step = Project.Presentation.Player.AimAssistMath.MagnetStep(1f, 1f, 1f);
            Assert.LessOrEqual(step, 1f);
            Assert.Greater(step, 0f);
            Assert.Less(Project.Presentation.Player.AimAssistMath.MagnetStep(-1f, 1f, 0.016f), 0f);
        }

        [Test]
        public void Assist_DisabledForMouse()
        {
            Assert.IsFalse(Project.Presentation.Player.AimAssistMath.IsActive(false, 100));
            Assert.IsFalse(Project.Presentation.Player.AimAssistMath.IsActive(true, 0));
            Assert.IsTrue(Project.Presentation.Player.AimAssistMath.IsActive(true, 1));
        }

        [Test]
        public void ToggleAds_LatchesOnPressAndReleasesWhenCannotAim()
        {
            var t = new Project.Presentation.Player.ToggleInputAdapter();
            Assert.IsFalse(t.ResolveAim(false, true, true));
            Assert.IsTrue(t.ResolveAim(true, true, true));
            Assert.IsTrue(t.ResolveAim(false, true, true));
            Assert.IsFalse(t.ResolveAim(true, true, true));
            t.ResolveAim(false, true, true);
            t.ResolveAim(true, true, true);
            Assert.IsFalse(t.ResolveAim(false, true, false));
        }

        [Test]
        public void AdsToggle_StateMachine_BufferedPressAndCancel()
        {
            var t = new Project.Presentation.Player.ToggleInputAdapter();
            // basış sırasında koşu: nişan yok, basış tamponlanır
            Assert.IsFalse(t.ResolveAim(true, true, false, 0.016f));
            Assert.IsTrue(t.PendingPress);
            Assert.IsFalse(t.ResolveAim(false, true, false, 0.016f));
            // koşu bitti: tamponlu basış nişanı açar
            Assert.IsTrue(t.ResolveAim(false, true, true, 0.016f));
            Assert.IsTrue(t.AdsLatched);
            // tek tık kapatır
            Assert.IsTrue(t.ResolveAim(false, true, true, 0.016f));
            Assert.IsFalse(t.ResolveAim(true, true, true, 0.016f));
            // tampon süresi dolarsa nişan açılmaz
            t.ResolveAim(false, true, true, 0.016f);
            t.ResolveAim(true, true, false, 0.016f);
            t.ResolveAim(false, true, false, 1f);
            Assert.IsFalse(t.ResolveAim(false, true, true, 0.016f));
            // şarjörde mandal düşer
            t.ResolveAim(true, true, true, 0.016f);
            Assert.IsFalse(t.ResolveAim(false, true, false, 0.016f));
            // Bas-Tut modu değişmez
            Assert.IsTrue(t.ResolveAim(true, false, true, 0.016f));
            Assert.IsFalse(t.ResolveAim(false, false, true, 0.016f));
            Assert.IsTrue(new Project.Core.Domain.GameSettings().ToggleAds);
        }

        [Test]
        public void ToggleCrouch_ConvertsHoldToEdge()
        {
            var t = new Project.Presentation.Player.ToggleInputAdapter();
            var down = new MovementInputState(0, 0, false, false, true);
            var a = t.AdaptMovement(down, true);
            Assert.IsTrue(a.CrouchToggle);
            Assert.IsFalse(a.Crouch);
            Assert.IsFalse(t.AdaptMovement(down, true).CrouchToggle);
        }

        [Test]
        public void ColorBlindPalettes_ChangeMarkerColors()
        {
            var prev = Project.Presentation.UI.UiTheme.ColorBlindPalette;
            try
            {
                Project.Presentation.UI.UiTheme.ColorBlindPalette = 0;
                var a0 = Project.Presentation.UI.UiTheme.AllyBlue;
                var e0 = Project.Presentation.UI.UiTheme.EnemyRed;
                for (var p = 1; p <= 3; p++)
                {
                    Project.Presentation.UI.UiTheme.ColorBlindPalette = p;
                    Assert.AreNotEqual(e0, Project.Presentation.UI.UiTheme.EnemyRed);
                    Assert.AreNotEqual(Project.Presentation.UI.UiTheme.AllyBlue, Project.Presentation.UI.UiTheme.EnemyRed);
                    Assert.AreNotEqual(a0, Project.Presentation.UI.UiTheme.EnemyRed);
                }
            }
            finally { Project.Presentation.UI.UiTheme.ColorBlindPalette = prev; }
        }

        [Test]
        public void SubtitleSizes_AreMonotonic()
        {
            for (var i = 1; i <= 3; i++)
                Assert.Greater(Project.Presentation.UI.RadioSubtitleView.FontSizeFor(i), Project.Presentation.UI.RadioSubtitleView.FontSizeFor(i - 1));
            Assert.AreEqual(Project.Presentation.UI.RadioSubtitleView.FontSizeFor(3), Project.Presentation.UI.RadioSubtitleView.FontSizeFor(99));
        }
#endif
    }
}
