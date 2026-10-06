using NUnit.Framework;
using Project.Presentation.UI.Lobby;

namespace Project.Tests
{
    public sealed class LobbyPlayPulseTests
    {
        [Test]
        public void PlayPulse_SinirlarIcindeVeDonguludur()
        {
            for (var i = 0; i < 100; i++)
            {
                var v = LobbyTheme.PlayPulse(i * 0.07f);
                Assert.IsTrue(v >= 0f && v <= 1f);
            }

            Assert.AreEqual(LobbyTheme.PlayPulse(0.3f), LobbyTheme.PlayPulse(1.9f), 0.001f);
        }
    }
}
