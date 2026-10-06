using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    public class ConvoyRulesTests
    {
        [Test]
        public void FirstArrival_DefendersWin()
        {
            var r = new ConvoyRules();
            r.MarkDestroyed(0);
            Assert.IsFalse(r.IsOver);
            r.MarkArrived(1);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(ConvoyRules.DefenderTeam, r.WinnerTeam);
        }

        [Test]
        public void AllDestroyed_AttackersWin()
        {
            var r = new ConvoyRules();
            for (var i = 0; i < r.Trucks; i++)
                r.MarkDestroyed(i);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(ConvoyRules.AttackerTeam, r.WinnerTeam);
        }

        [Test]
        public void DefendersEliminated_AttackersWin_OnlyAfterSeen()
        {
            var r = new ConvoyRules();
            r.NotifyDefendersAlive(0);
            Assert.IsFalse(r.IsOver);
            r.NotifyDefendersAlive(5);
            r.NotifyDefendersAlive(0);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(ConvoyRules.AttackerTeam, r.WinnerTeam);
        }

        [Test]
        public void TimeUp_AttackersWin_AndResultIsFinal()
        {
            var r = new ConvoyRules(3, 10f);
            r.Tick(10f);
            Assert.AreEqual(ConvoyRules.AttackerTeam, r.WinnerTeam);
            r.MarkArrived(0);
            Assert.AreEqual(ConvoyRules.AttackerTeam, r.WinnerTeam);
        }

        [Test]
        public void TruckState_IsNotChangedTwice()
        {
            var r = new ConvoyRules();
            r.MarkDestroyed(0);
            r.MarkDestroyed(0);
            Assert.AreEqual(1, r.Destroyed);
            Assert.AreEqual(2, r.Rolling);
        }

        [Test]
        public void Damage_FriendlyFireIgnored()
        {
            Assert.AreEqual(0f, ConvoyRules.TruckDamage(50f, ConvoyRules.DefenderTeam));
            Assert.AreEqual(50f, ConvoyRules.TruckDamage(50f, ConvoyRules.AttackerTeam));
            Assert.AreEqual(0f, ConvoyRules.TruckDamage(float.NaN, 1));
            Assert.AreEqual(0f, ConvoyRules.AmbushDamage(0, 1f));
            Assert.Greater(ConvoyRules.AmbushDamage(3, 1f), 0f);
        }

        [Test]
        public void Route_LengthSampleSlice()
        {
            var xs = new[] { 0f, 100f, 100f };
            var zs = new[] { 0f, 0f, 100f };
            Assert.AreEqual(200f, ConvoyRoute.Length(xs, zs), 0.01f);

            Assert.IsTrue(ConvoyRoute.Sample(xs, zs, 150f, out var x, out var z, out var yaw));
            Assert.AreEqual(100f, x, 0.01f);
            Assert.AreEqual(50f, z, 0.01f);
            Assert.AreEqual(0f, yaw, 0.01f);

            ConvoyRoute.Sample(xs, zs, 50f, out _, out _, out yaw);
            Assert.AreEqual(90f, yaw, 0.01f);

            ConvoyRoute.Sample(xs, zs, 999f, out x, out z, out _);
            Assert.AreEqual(100f, x, 0.01f);
            Assert.AreEqual(100f, z, 0.01f);

            var sx = new System.Collections.Generic.List<float>();
            var sz = new System.Collections.Generic.List<float>();
            ConvoyRoute.Slice(xs, zs, 0.25f, 0.75f, sx, sz);
            Assert.AreEqual(3, sx.Count);
            Assert.AreEqual(50f, sx[0], 0.01f);
            Assert.AreEqual(100f, sx[1], 0.01f);
            Assert.AreEqual(50f, sz[2], 0.01f);
        }
    }
}
