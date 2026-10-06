using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class SkirmishRulesTests
    {
        [Test]
        public void FirstTeamToTarget_Wins()
        {
            var r = new SkirmishRules(3, 600f, 20f);
            r.RegisterKill(0, 1);
            r.RegisterKill(1, 0);
            r.RegisterKill(0, 1);
            Assert.IsFalse(r.IsOver);
            r.RegisterKill(0, 1);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(0, r.WinnerTeam);
            Assert.AreEqual(3, r.GetKills(0));
        }

        [Test]
        public void FriendlyKills_AreIgnored()
        {
            var r = new SkirmishRules(5, 600f, 20f);
            r.RegisterKill(0, 0);
            r.RegisterKill(-1, 1);
            Assert.AreEqual(0, r.GetKills(0));
        }

        [Test]
        public void TimeUp_LeaderWinsOrDraw()
        {
            var r = new SkirmishRules(50, 10f, 20f);
            r.RegisterKill(1, 0);
            r.Tick(10f);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(1, r.WinnerTeam);

            var d = new SkirmishRules(50, 10f, 20f);
            d.Tick(11f);
            Assert.AreEqual(-1, d.WinnerTeam);
            Assert.IsTrue(d.IsOver);
        }

        [Test]
        public void Waves_FireEveryInterval()
        {
            var r = new SkirmishRules(50, 600f, 20f);
            var waves = 0;
            for (var i = 0; i < 100; i++)
                if (r.Tick(1f))
                    waves++;
            Assert.AreEqual(5, waves);
        }
    }
}
