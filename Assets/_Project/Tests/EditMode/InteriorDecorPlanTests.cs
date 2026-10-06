using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class InteriorDecorPlanTests
    {
        private static readonly Rect Room = new Rect(0f, 0f, 5f, 4f);

        [Test]
        public void Plan_IsDeterministic_AndWithinBudget()
        {
            for (var k = 0; k < 4; k++)
            {
                var a = new List<DecorItem>();
                var b = new List<DecorItem>();
                var none = new List<FurniturePlacement>();
                InteriorDecorPlan.Plan(Room, (RoomKind)k, 2.6f, new System.Random(5), null, none, a);
                InteriorDecorPlan.Plan(Room, (RoomKind)k, 2.6f, new System.Random(5), null, none, b);
                Assert.AreEqual(a.Count, b.Count);
                var tris = 0;
                for (var i = 0; i < a.Count; i++)
                    tris += InteriorDecorPlan.TriCost(a[i].Kind);
                Assert.LessOrEqual(tris, InteriorDecorPlan.RoomTriBudget);
            }
        }

        [Test]
        public void Hangings_AvoidDoorZones()
        {
            var door = new Rect(0f, 1.5f, 1f, 1f);
            for (var s = 0; s < 40; s++)
            {
                var items = new List<DecorItem>();
                InteriorDecorPlan.Plan(Room, RoomKind.LivingRoom, 2.6f, new System.Random(s), new[] { door }, new List<FurniturePlacement>(), items);
                foreach (var it in items)
                {
                    if (it.Kind == DecorKind.Cobweb)
                        continue;
                    Assert.IsFalse(InteriorDecorPlan.WallFootprint(Room, it.Wall, it.U, it.Width).Overlaps(door));
                }
            }
        }

        [Test]
        public void StorageHasNoHangings_ButHasWeb()
        {
            var items = new List<DecorItem>();
            InteriorDecorPlan.Plan(Room, RoomKind.Storage, 2.6f, new System.Random(1), null, new List<FurniturePlacement>(), items);
            Assert.IsTrue(items.TrueForAll(i => i.Kind == DecorKind.Cobweb));
            Assert.Greater(items.Count, 0);
        }

        [Test]
        public void BulbOn_NightAndNearOnly()
        {
            Assert.IsTrue(InteriorDecorPlan.BulbOn(0.4f, 5f));
            Assert.IsFalse(InteriorDecorPlan.BulbOn(-0.8f, 5f));
            Assert.IsFalse(InteriorDecorPlan.BulbOn(0.4f, 40f));
        }

        [Test]
        public void LivingRoom_PlansSedirAndStove_WithoutOverlap()
        {
            var list = new List<FurniturePlacement>();
            InteriorLayout.Plan(new Rect(0f, 0f, 5f, 4f), RoomKind.LivingRoom, new System.Random(3), null, list);
            for (var i = 0; i < list.Count; i++)
                for (var j = i + 1; j < list.Count; j++)
                    if (list[i].Blocks && list[j].Blocks)
                        Assert.IsFalse(list[i].Footprint.Overlaps(list[j].Footprint), list[i].Kind + " / " + list[j].Kind);
        }
    }
}
