using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MatchSummaryTests
    {
        private static CombatantResult C(int id, int team, int teamPlacement, int placement, int kills) =>
            new CombatantResult(new PlayerId(id), "A" + id, team, "Tim" + team, teamPlacement, placement, kills, 0, 10f * kills, 5f, teamPlacement == 1);

        [Test]
        public void GroupByTeam_SortsTeamsByPlacementAndSumsKills()
        {
            var summary = new MatchSummary(new[]
            {
                C(1, 0, 2, 5, 1), C(2, 1, 1, 1, 3), C(3, 0, 2, 4, 2), C(4, 1, 1, 2, 0)
            }, 1, 300f, false);

            var teams = summary.GroupByTeam();

            Assert.AreEqual(2, teams.Count);
            Assert.AreEqual(1, teams[0].Team);
            Assert.AreEqual(1, teams[0].Placement);
            Assert.AreEqual(3, teams[0].Kills);
            Assert.AreEqual(2, teams[0].Members.Count);
            Assert.AreEqual(4, teams[0].Members[1].Id.Value); // bireysel sıraya göre ikinci üye
            Assert.AreEqual(0, teams[1].Team);
            Assert.AreEqual(3, teams[1].Kills);
        }

        [Test]
        public void GroupByTeam_TeamlessCombatantsBecomeSoloTeams()
        {
            var summary = new MatchSummary(new[] { C(1, -1, 0, 3, 0), C(2, -1, 0, 1, 2) }, -1, 10f, true);
            var teams = summary.GroupByTeam();
            Assert.AreEqual(2, teams.Count);
            Assert.AreEqual(1, teams[0].Placement);
            Assert.AreEqual(1, teams[0].Members.Count);
            Assert.IsTrue(summary.TimedOut);
        }

        [Test]
        public void EmptySummary_HasNoTeams()
        {
            Assert.AreEqual(0, new MatchSummary(null, -1, 0f, false).GroupByTeam().Count);
        }
    }
}
