#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    public class ScoreboardRulesTests
    {
        [Test]
        public void Teams_AliveFirst_ThenPlacement()
        {
            var list = new List<ScoreboardTeamKey>
            {
                new ScoreboardTeamKey { Team = 0, Alive = 0, Placement = 3 },
                new ScoreboardTeamKey { Team = 1, Alive = 2, Placement = 0 },
                new ScoreboardTeamKey { Team = 2, Alive = 0, Placement = 2 },
                new ScoreboardTeamKey { Team = 3, Alive = 4, Placement = 0 },
            };
            list.Sort(ScoreboardRules.TeamOrder);
            Assert.AreEqual(3, list[0].Team);
            Assert.AreEqual(1, list[1].Team);
            Assert.AreEqual(2, list[2].Team);
            Assert.AreEqual(0, list[3].Team);
        }

        [Test]
        public void Members_AliveThenKills()
        {
            var list = new List<ScoreboardMemberKey>
            {
                new ScoreboardMemberKey { Index = 0, Status = ScoreboardStatus.Dead, Kills = 9 },
                new ScoreboardMemberKey { Index = 1, Status = ScoreboardStatus.Alive, Kills = 1 },
                new ScoreboardMemberKey { Index = 2, Status = ScoreboardStatus.Alive, Kills = 3 },
            };
            list.Sort(ScoreboardRules.MemberOrder);
            Assert.AreEqual(2, list[0].Index);
            Assert.AreEqual(1, list[1].Index);
            Assert.AreEqual(0, list[2].Index);
        }

        [Test]
        public void RoleCodes_AreDistinct()
        {
            var seen = new HashSet<string>();
            foreach (TeamRole r in System.Enum.GetValues(typeof(TeamRole)))
                Assert.IsTrue(seen.Add(ScoreboardRules.RoleCode(r)));
        }
    }
}
#endif
