using NUnit.Framework;
using Project.Application.AI;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class TeammateTacticsTests
    {
        [Test]
        public void YuksekSiper_AlcakSiperdenIyi()
        {
            var self = new Float3(0, 0, 0);
            var threat = new Float3(0, 0, 30);
            var hi = new TeammateCoverScoring.Candidate(new Float3(0, 0, 6), 1.8f);
            var lo = new TeammateCoverScoring.Candidate(new Float3(0, 0, 6), 0.5f);
            Assert.Greater(TeammateCoverScoring.Score(self, threat, hi), TeammateCoverScoring.Score(self, threat, lo));
        }

        [Test]
        public void SiperYoksa_PickBestEksi()
        {
            var c = new[] { new TeammateCoverScoring.Candidate(new Float3(0, 0, 5), 0.1f) };
            Assert.AreEqual(-1, TeammateCoverScoring.PickBest(new Float3(0, 0, 0), new Float3(0, 0, 30), c, 1), 0f);
        }

        [Test]
        public void AtesAltinda_YaraliyaOrtme()
        {
            var a = TeammateAid.Decide(new Float3(0, 0, 0), new Float3(0, 0, 8), true, true, true, false, 1f);
            Assert.AreEqual((int)AidAction.CoverWounded, (int)a, 0f);
        }

        [Test]
        public void BosSarjor_Bildirim()
        {
            Assert.AreEqual((int)CalloutKind.MagEmpty, (int)TeammateCallouts.MagStatus(0, 30, 2), 0f);
        }
    }
}
