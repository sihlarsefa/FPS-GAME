#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Characters;

namespace Project.Tests.EditMode
{
    public sealed class SoldierMeshCombinerRulesTests
    {
        private static List<CombineCandidate> Make(int n, string name = "Part", int parent = 0, int mat = 0)
        {
            var l = new List<CombineCandidate>();
            for (var i = 0; i < n; i++)
                l.Add(new CombineCandidate { Name = name, ParentId = parent, MaterialId = mat, CastShadows = true });
            return l;
        }

        [Test]
        public void BelowThreshold_NoGroups() =>
            Assert.AreEqual(0, SoldierMeshCombinerRules.Plan(Make(SoldierMeshCombinerRules.MinPartCount)).Count);

        [Test]
        public void SameKey_OneGroup()
        {
            var g = SoldierMeshCombinerRules.Plan(Make(40));
            Assert.AreEqual(1, g.Count);
            Assert.AreEqual(40, g[0].Count);
        }

        [Test]
        public void DifferentParentMaterialShadow_SplitGroups()
        {
            var l = Make(10, "A", 0, 0);
            l.AddRange(Make(10, "A", 1, 0));
            l.AddRange(Make(10, "A", 0, 1));
            var s = Make(10, "A", 0, 0);
            for (var i = 0; i < s.Count; i++) { var c = s[i]; c.CastShadows = false; s[i] = c; }
            l.AddRange(s);
            Assert.AreEqual(4, SoldierMeshCombinerRules.Plan(l).Count);
        }

        [Test]
        public void Excluded_Names_AreSkipped()
        {
            foreach (var n in new[] { "Armband", "Skull", "ChestShape", "Eye", "Helmet", "HelmetRim", "Pack", "PackDome", "Beret", "RankPatch", "Vest" })
                Assert.IsTrue(SoldierMeshCombinerRules.IsExcluded(n), n);
            foreach (var n in new[] { "Pelvis", "Thigh", "Glove", "Hair", "KneePad", "FlagPatch" })
                Assert.IsFalse(SoldierMeshCombinerRules.IsExcluded(n), n);
            var l = Make(35, "Armband");
            Assert.AreEqual(0, SoldierMeshCombinerRules.Plan(l).Count);
        }

        [Test]
        public void SingletonGroups_Dropped()
        {
            var l = Make(31, "Part", 0, 0);
            var c = l[0]; c.MaterialId = 9; l[0] = c;
            var g = SoldierMeshCombinerRules.Plan(l);
            Assert.AreEqual(1, g.Count);
            Assert.AreEqual(30, g[0].Count);
        }
    }
}
#endif
