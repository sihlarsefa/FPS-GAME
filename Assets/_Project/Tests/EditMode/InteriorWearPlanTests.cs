using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class InteriorWearPlanTests
    {
        [Test]
        public void YogunlukKademeyleArtar()
        {
            for (var t = 0; t < 3; t++)
            {
                Assert.IsTrue(InteriorWearPlan.Density(t + 1) > InteriorWearPlan.Density(t));
                Assert.IsTrue(InteriorWearPlan.BulletHoles(10f, t + 1, false) >= InteriorWearPlan.BulletHoles(10f, t, false));
                Assert.IsTrue(InteriorWearPlan.MaxShafts(t + 1) >= InteriorWearPlan.MaxShafts(t));
                Assert.IsTrue(InteriorWearPlan.MaxPiecesPerRoom(t + 1) >= InteriorWearPlan.MaxPiecesPerRoom(t));
            }
        }

        [Test]
        public void EnDusukKademedeHuzmeYok()
        {
            Assert.AreEqual(0, InteriorWearPlan.MaxShafts(0));
            Assert.AreEqual(0, InteriorWearPlan.MaxShafts(-5));
            Assert.AreEqual(InteriorWearPlan.MaxShafts(3), InteriorWearPlan.MaxShafts(99));
        }

        [Test]
        public void YikikBinaDahaCokHasarli()
        {
            Assert.IsTrue(InteriorWearPlan.BulletHoles(12f, 3, true) > InteriorWearPlan.BulletHoles(12f, 3, false));
            Assert.IsTrue(InteriorWearPlan.Shards(2, true) > InteriorWearPlan.Shards(2, false));
            Assert.IsTrue(InteriorWearPlan.TipChance(2, true) > InteriorWearPlan.TipChance(2, false));
        }

        [Test]
        public void NegatifVeSifirGirdiGuvenli()
        {
            Assert.AreEqual(0, InteriorWearPlan.BulletHoles(-3f, 3, true));
            Assert.AreEqual(0, InteriorWearPlan.Debris(0f, 3, true));
            Assert.AreEqual(0, InteriorWearPlan.SoldierItems(0f, 0, false));
        }

        [Test]
        public void RastgeleNoktaOdaIcindeVeDeterministik()
        {
            var r = Rect.MinMaxRect(1f, 2f, 5f, 6f);
            var a = new System.Random(5);
            var b = new System.Random(5);
            for (var i = 0; i < 20; i++)
            {
                var p = InteriorWearPlan.RandomIn(r, a, 0.5f);
                var q = InteriorWearPlan.RandomIn(r, b, 0.5f);
                Assert.IsTrue(p.x >= 1.49f && p.x <= 4.51f && p.y >= 2.49f && p.y <= 5.51f);
                Assert.AreEqual(p.x, q.x, 0.0001f);
            }

            var tiny = InteriorWearPlan.RandomIn(Rect.MinMaxRect(0f, 0f, 0.4f, 0.4f), a, 0.5f);
            Assert.AreEqual(0.2f, tiny.x, 0.001f);
        }
    }
}
