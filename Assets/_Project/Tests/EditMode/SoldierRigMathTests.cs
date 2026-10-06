#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class SoldierRigMathTests
    {
        [Test]
        public void StandingSegments_AreOrderedHeadToFoot()
        {
            var s = SoldierRigMath.StandingSegments();
            Assert.Less(s[4].Max, s[2].Max, "kalça göğüsten alçak");
            Assert.Less(s[2].Max, s[0].Max, "göğüs baştan alçak");
            Assert.Less(s[6].Max, s[5].Max, "baldır uyluktan alçak");
            Assert.Less(s[5].Min, s[4].Min, "uyluk kalçadan aşağı uzanır");
            Assert.Less(SoldierRigMath.StandingAnkleY(), s[6].Max, "ayak dizden alçak");
        }

        [Test]
        public void StandingSegments_NoGapOver25cm()
        {
            var s = SoldierRigMath.StandingSegments();
            // Baş > boyun > göğüs > karın > kalça > uyluk > baldır: komşu parçalar arasındaki boşluk (alt - üst) < 0.25 m.
            for (var i = 0; i < s.Length - 1; i++)
            {
                var gap = s[i].Min - s[i + 1].Max;
                Assert.Less(gap, 0.25f, s[i].Name + " ile " + s[i + 1].Name + " arası boşluk " + gap);
            }

            Assert.Less(s[s.Length - 1].Min - SoldierRigMath.StandingAnkleY(), 0.25f, "baldır ayak bileğine kadar iner");
        }

        [Test]
        public void StandingAnkle_ReachableAndAboveGround()
        {
            var y = SoldierRigMath.StandingAnkleY();
            Assert.GreaterOrEqual(y, 0f);
            Assert.Less(y, 0.15f);
        }

        [Test]
        public void IsFinite_RejectsNaN()
        {
            Assert.IsTrue(SoldierRigMath.IsFinite(Quaternion.identity));
            Assert.IsFalse(SoldierRigMath.IsFinite(new Quaternion(float.NaN, 0f, 0f, 1f)));
        }
    }
}
#endif
