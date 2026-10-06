using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class ReconDroneServiceTests
    {
        [Test]
        public void Launch_StartsCooldownPerTeam()
        {
            var s = new ReconDroneService();
            Assert.IsTrue(s.TryLaunch(1, 10f));
            Assert.IsFalse(s.TryLaunch(1, 50f));
            Assert.AreEqual(80f, s.GetCooldownRemaining(1, 50f), 0.001f);
            Assert.IsTrue(s.TryLaunch(2, 50f));
            Assert.IsTrue(s.TryLaunch(1, 130f));
        }

        [Test]
        public void SelectEnemies_FiltersTeamAliveAndRadius()
        {
            var s = new ReconDroneService();
            var pos = new List<Float3> { new Float3(10, 0, 0), new Float3(70, 0, 0), new Float3(0, 0, 30), new Float3(5, 0, 5), new Float3(0, 90, 59) };
            var teams = new List<int> { 2, 2, 1, 3, 2 };
            var alive = new List<bool> { true, true, true, false, true };
            var res = new List<int>();
            var n = s.SelectEnemies(new Float3(0, 0, 0), 1, pos, teams, alive, res);
            Assert.AreEqual(2, n);
            Assert.AreEqual(0, res[0]);
            Assert.AreEqual(4, res[1]);
        }
    }
}
