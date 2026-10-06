#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.AI;
using UnityEngine;

namespace Project.Tests
{
    public class BotSquadTacticsTests
    {
        [Test]
        public void FireTeams_Alternate()
        {
            Assert.AreEqual(0, BotSquadTactics.FireTeam(0));
            Assert.AreEqual(1, BotSquadTactics.FireTeam(1));
            Assert.AreEqual(0, BotSquadTactics.FireTeam(2));
        }

        [Test]
        public void FlankAngle_InRange()
        {
            for (var i = 0; i < 10; i++)
            {
                var a = BotSquadTactics.FlankAngle(i, 0.5f);
                Assert.GreaterOrEqual(a, 30f);
                Assert.LessOrEqual(a, 60f);
            }
        }

        [Test]
        public void FlankPoint_OffsetByAngle()
        {
            var enemy = Vector3.zero;
            var self = new Vector3(0f, 0f, -40f);
            var p = BotSquadTactics.FlankPoint(self, enemy, 45f, 1f, 40f);
            Assert.AreEqual(40f, Vector3.Distance(p, enemy), 0.01f);
            Assert.AreEqual(45f, Vector3.Angle(self - enemy, p - enemy), 0.1f);
            var q = BotSquadTactics.FlankPoint(self, enemy, 45f, -1f, 40f);
            Assert.Less(p.x * q.x, 0f);
        }

        [Test]
        public void Flank_OnlyManeuverTeamHealthyMidRange()
        {
            Assert.IsFalse(BotSquadTactics.ShouldFlank(2, 40f, 1f, 1f, 0f));
            Assert.IsFalse(BotSquadTactics.ShouldFlank(1, 40f, 0.4f, 1f, 0f));
            Assert.IsFalse(BotSquadTactics.ShouldFlank(1, 10f, 1f, 1f, 0f));
            Assert.IsTrue(BotSquadTactics.ShouldFlank(1, 40f, 1f, 1f, 0f));
        }

        [Test]
        public void Suppress_Window()
        {
            Assert.IsTrue(BotSquadTactics.ShouldSuppress(1.5f, true, 0.8f, 0.9f));
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(5f, true, 0.8f, 0.9f));
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(1.5f, false, 0.8f, 0.9f));
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(1.5f, true, 0.1f, 0.9f));
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(1.5f, true, 0.8f, 0.2f));
        }

        [Test]
        public void Bounding_TeamsTakeTurns()
        {
            Assert.AreNotEqual(BotSquadTactics.MovesThisPhase(1, 1f), BotSquadTactics.MovesThisPhase(2, 1f));
            Assert.AreNotEqual(BotSquadTactics.MovesThisPhase(1, 1f), BotSquadTactics.MovesThisPhase(1, 5f));
        }

        [Test]
        public void Grenade_CoverRangeAndAllies()
        {
            Assert.IsTrue(BotSquadTactics.GrenadeWorthwhile(20f, true, 30f, 6f));
            Assert.IsFalse(BotSquadTactics.GrenadeWorthwhile(30f, true, 30f, 6f));
            Assert.IsFalse(BotSquadTactics.GrenadeWorthwhile(20f, false, 30f, 6f));
            Assert.IsFalse(BotSquadTactics.GrenadeWorthwhile(20f, true, 5f, 6f));
        }

        [Test]
        public void Retreat_NeedsAllConditions()
        {
            Assert.IsTrue(BotSquadTactics.ShouldRetreatToHeal(0.2f, true, true));
            Assert.IsFalse(BotSquadTactics.ShouldRetreatToHeal(0.5f, true, true));
            Assert.IsFalse(BotSquadTactics.ShouldRetreatToHeal(0.2f, false, true));
            Assert.IsFalse(BotSquadTactics.ShouldRetreatToHeal(0.2f, true, false));
        }
    }
}
#endif
