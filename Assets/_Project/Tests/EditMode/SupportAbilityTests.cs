using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class SupportAbilityTests
    {
        [Test]
        public void NotReadyAtStart_ReadyAfterCooldown()
        {
            var s = new SupportAbilityService();
            Assert.IsFalse(s.IsReady(1, 0f));
            Assert.AreEqual(300f, s.GetCooldownRemaining(1, 0f), 0.01f);
            Assert.IsFalse(s.IsReady(1, 299f));
            Assert.IsTrue(s.IsReady(1, 300f));
        }

        [Test]
        public void EightKills_UnlockEarly()
        {
            var s = new SupportAbilityService();
            for (var i = 0; i < 7; i++) s.RegisterKill(2, 10f);
            Assert.IsFalse(s.IsReady(2, 10f));
            s.RegisterKill(2, 10f);
            Assert.IsTrue(s.IsReady(2, 10f));
            Assert.AreEqual(0f, s.GetCooldownRemaining(2, 10f));
        }

        [Test]
        public void Activate_ResetsKills_StartsCooldown_BlocksWhileActive()
        {
            var s = new SupportAbilityService();
            for (var i = 0; i < 8; i++) s.RegisterKill(0, 5f);
            Assert.IsTrue(s.TryActivate(0, 5f));
            Assert.IsTrue(s.IsActive(0));
            Assert.AreEqual(0, s.GetKills(0, 5f));
            Assert.IsFalse(s.TryActivate(0, 6f));
            s.EndActive(0);
            Assert.IsFalse(s.IsReady(0, 100f));
            Assert.IsTrue(s.IsReady(0, 305f));
        }

        [Test]
        public void ActiveTeam_DoesNotBecomeReadyEvenWithKills()
        {
            var s = new SupportAbilityService();
            for (var i = 0; i < 8; i++) s.RegisterKill(0, 5f);
            s.TryActivate(0, 5f);
            for (var i = 0; i < 8; i++) s.RegisterKill(0, 6f);
            Assert.IsFalse(s.IsReady(0, 6f));
            s.EndActive(0);
            Assert.IsTrue(s.IsReady(0, 6f));
        }

        [Test]
        public void TeamsAreIndependent_AndInvalidTeamIgnored()
        {
            var s = new SupportAbilityService();
            for (var i = 0; i < 8; i++) s.RegisterKill(1, 0f);
            s.RegisterKill(-1, 0f);
            Assert.IsTrue(s.IsReady(1, 0f));
            Assert.IsFalse(s.IsReady(2, 0f));
            Assert.IsFalse(s.IsReady(-1, 1000f));
            Assert.IsFalse(s.TryActivate(-1, 1000f));
        }

        [Test]
        public void ReadyNotice_FiresOnce()
        {
            var s = new SupportAbilityService();
            Assert.IsFalse(s.ConsumeReadyNotice(3, 0f));
            Assert.IsTrue(s.ConsumeReadyNotice(3, 301f));
            Assert.IsFalse(s.ConsumeReadyNotice(3, 302f));
            s.TryActivate(3, 302f);
            s.EndActive(3);
            Assert.IsTrue(s.ConsumeReadyNotice(3, 700f));
        }

        [Test]
        public void Reset_ClearsState()
        {
            var s = new SupportAbilityService();
            for (var i = 0; i < 8; i++) s.RegisterKill(0, 0f);
            s.Reset();
            Assert.IsFalse(s.IsReady(0, 0f));
        }

        [Test]
        public void Health_DiesAtZero_IgnoresBadDamage()
        {
            var h = new SupportHeliHealth();
            Assert.AreEqual(1500f, h.Max);
            Assert.IsFalse(h.Damage(-5f));
            Assert.IsFalse(h.Damage(float.NaN));
            Assert.IsFalse(h.Damage(1000f));
            Assert.IsTrue(h.Damage(600f));
            Assert.IsTrue(h.IsDown);
            Assert.AreEqual(0f, h.Current);
            Assert.IsFalse(h.Damage(10f));
        }

        [Test]
        public void SalvoTimes_InsideOrbit()
        {
            var t0 = SupportAbilityService.SalvoTime(0, 25f);
            var t1 = SupportAbilityService.SalvoTime(1, 25f);
            Assert.Less(0f, t0);
            Assert.Less(t0, t1);
            Assert.Less(t1, 25f);
            Assert.AreEqual(-1f, SupportAbilityService.SalvoTime(2, 25f));
        }

        [Test]
        public void OrbitPoint_StaysOnRadius()
        {
            SupportAbilityService.OrbitPoint(100f, 50f, 40f, 1.2f, out var x, out var z);
            var d = (float)System.Math.Sqrt((x - 100f) * (x - 100f) + (z - 50f) * (z - 50f));
            Assert.AreEqual(40f, d, 0.001f);
        }

        [Test]
        public void AttackHeliBinding_DefaultsToFreeKey_NoConflicts()
        {
            var map = new InputBindingMap();
            Assert.IsTrue(map.Has(BindAction.AttackHeli, "L"));
            Assert.IsFalse(ControlScheme.IsReserved("L"));
            Assert.AreEqual(0, ControlScheme.FindConflicts(ControlScheme.BuildEntries(map)).Count);
        }
    }
}
