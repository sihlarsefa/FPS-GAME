#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RadioVoiceTests
    {
        [TestCase("Leader")]
        [TestCase("Rifleman")]
        [TestCase("Marksman")]
        [TestCase("MachineGunner")]
        [TestCase("Medic")]
        [TestCase("Radioman")]
        [TestCase("Grenadier")]
        [TestCase("Bilinmeyen")]
        public void PitchForRole_StaysInRange(string role)
        {
            for (var seed = -3; seed < 20; seed++)
            {
                var p = RadioVoicePlayer.PitchForRole(role, seed);
                Assert.GreaterOrEqual(p, 0.85f);
                Assert.LessOrEqual(p, 0.95f);
            }
        }

        [Test]
        public void PlayerKey_BuildsExpectedKeys()
        {
            Assert.AreEqual("player_reload_1", RadioVoicePlayer.PlayerKey("reload", 0.1f));
            Assert.AreEqual("player_grenade_2", RadioVoicePlayer.PlayerKey("grenade", 0.9f));
            Assert.IsNull(RadioVoicePlayer.PlayerKey("", 0.5f));
        }
    }
}
#endif
