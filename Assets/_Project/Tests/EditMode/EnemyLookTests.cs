using NUnit.Framework;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class EnemyLookTests
    {
        [Test]
        public void MakeEnemy_DarkensAndRedBand_KeepsCamo()
        {
            var f = SoldierLook.ForTeam(0, null);
            var e = SoldierLook.ForTeam(0, null).MakeEnemy();
            Assert.IsTrue(e.IsEnemy);
            Assert.Less(e.CamoA.g, f.CamoA.g);
            Assert.AreEqual(f.CamoSeed, e.CamoSeed);
            Assert.AreEqual(f.CamoPattern, e.CamoPattern);
            Assert.AreEqual(SoldierLook.EnemyBandColor, e.Armband);
        }

        [Test]
        public void MakeEnemy_Idempotent_AndCloned()
        {
            var e = SoldierLook.ForEnemyTeam(2, null);
            var c = e.Color32Safe();
            e.MakeEnemy();
            Assert.AreEqual(c, e.CamoA);
            Assert.IsTrue(e.Clone().IsEnemy);
        }

        [Test]
        public void BandPixels_HaveDiagonalStripes_Deterministic()
        {
            var a = SoldierLook.EnemyBandPixels(32);
            var b = SoldierLook.EnemyBandPixels(32);
            Assert.AreEqual(32 * 32, a.Length);
            var white = 0;
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].r, b[i].r);
                if (a[i].g > 200) white++;
            }
            Assert.Greater(white, 100);
            Assert.AreEqual(a[0 * 32 + 5].g, a[1 * 32 + 4].g); // çapraz: x+y sabit
        }
    }

    internal static class EnemyLookTestExt
    {
        public static Color Color32Safe(this SoldierLook l) => l.CamoA;
    }
}
