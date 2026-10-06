using NUnit.Framework;
using Project.Presentation.UI.Lobby;

namespace Project.Tests.EditMode
{
    public sealed class LobbyThemeTests
    {
        [Test]
        public void Ease_EndpointsAreExact()
        {
            Assert.AreEqual(0f, LobbyTheme.EaseOutCubic(0f));
            Assert.AreEqual(1f, LobbyTheme.EaseOutCubic(1f));
            Assert.AreEqual(0f, LobbyTheme.EaseInOutCubic(0f));
            Assert.AreEqual(1f, LobbyTheme.EaseInOutCubic(1f));
        }

        [Test]
        public void WrapTab_IsCircular()
        {
            Assert.AreEqual(3, LobbyTheme.WrapTab(0, -1));
            Assert.AreEqual(0, LobbyTheme.WrapTab(3, 1));
            Assert.AreEqual(2, LobbyTheme.WrapTab(1, 1));
        }

        [Test]
        public void TabCenter_StaysInsideBar()
        {
            Assert.AreEqual(0.125f, LobbyTheme.TabCenter01(0), 0.0001f);
            Assert.AreEqual(0.875f, LobbyTheme.TabCenter01(99), 0.0001f);
        }

        [Test]
        public void PanelPose_EnterEndsSettled_ExitEndsHidden()
        {
            LobbyTheme.PanelPose(1f, true, out var a, out var x);
            Assert.AreEqual(1f, a, 0.0001f);
            Assert.AreEqual(0f, x, 0.0001f);
            LobbyTheme.PanelPose(1f, false, out a, out x);
            Assert.AreEqual(0f, a, 0.0001f);
            LobbyTheme.PanelPose(0f, true, out a, out x);
            Assert.AreEqual(0f, a, 0.0001f);
            Assert.Greater(x, 0f);
        }

        [Test]
        public void Glow_StepsToTargetAndStays()
        {
            var h = 0f;
            for (var i = 0; i < 100; i++) h = LobbyTheme.StepToward(h, 1f, 0.02f);
            Assert.AreEqual(1f, h, 0.0001f);
            Assert.Greater(LobbyTheme.GlowAlpha(1f, 0f), LobbyTheme.GlowAlpha(0.2f, 0f));
            Assert.AreEqual(0f, LobbyTheme.GlowAlpha(0f, 0f), 0.0001f);
        }

        [Test]
        public void Vignette_PulseBounded()
        {
            for (var t = 0f; t < 12f; t += 0.37f)
            {
                var a = LobbyTheme.VignetteAlpha(t);
                Assert.IsTrue(a >= 0.4f && a <= 0.7f);
            }
        }

        [Test]
        public void Stats_HandleZeroes()
        {
            Assert.AreEqual(5f, LobbyTheme.KdRatio(5, 0), 0.0001f);
            Assert.AreEqual(2f, LobbyTheme.KdRatio(10, 5), 0.0001f);
            Assert.AreEqual(0, LobbyTheme.WinPercent(3, 0));
            Assert.AreEqual(50, LobbyTheme.WinPercent(1, 2));
        }
    }
}
