using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class InteriorUnifiedTests
    {
        private static readonly Rect Room = new Rect(0f, 0f, 5f, 4f);

        [Test]
        public void PlanRoomAyniTohumAyniYerlesim()
        {
            var a = new List<FurniturePlacement>();
            var b = new List<FurniturePlacement>();
            InteriorFurnisher.PlanRoom(Room, RoomKind.LivingRoom, 42, null, 9, a);
            InteriorFurnisher.PlanRoom(Room, RoomKind.LivingRoom, 42, null, 9, b);
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i].Footprint.x, b[i].Footprint.x, 0.0001f);
        }

        [Test]
        public void PlanRoomParcaSiniriniUygular()
        {
            var a = new List<FurniturePlacement>();
            InteriorFurnisher.PlanRoom(Room, RoomKind.Bedroom, 7, null, 2, a);
            Assert.IsTrue(a.Count <= 2);
        }

        [Test]
        public void PlanRoomSinirsizTumParcalari()
        {
            var a = new List<FurniturePlacement>();
            var b = new List<FurniturePlacement>();
            InteriorFurnisher.PlanRoom(Room, RoomKind.Kitchen, 3, null, -1, a);
            InteriorFurnisher.PlanRoom(Room, RoomKind.Kitchen, 3, null, 99, b);
            Assert.AreEqual(a.Count, b.Count);
        }

        [Test]
        public void TohumKararliVeOdayaGoreFarkli()
        {
            Assert.AreEqual(InteriorWearPlan.SeedFor(5, 0, 1), InteriorWearPlan.SeedFor(5, 0, 1));
            Assert.IsTrue(InteriorWearPlan.SeedFor(5, 0, 1) != InteriorWearPlan.SeedFor(5, 0, 2));
            Assert.IsTrue(InteriorWearPlan.SeedFor(5, 0, 1) != InteriorWearPlan.SeedFor(5, 1, 1));
        }

        [Test]
        public void AskerEsyasiTuruAraliktaKalir()
        {
            for (var i = -9; i < 20; i++)
            {
                var k = InteriorWearPlan.ItemKind(i);
                Assert.IsTrue(k >= 0 && k <= 3);
            }
        }

        [Test]
        public void CamKirigiSayisiButceyiAsmaz()
        {
            var limits = new[] { 28, 50, 72, 96 };
            for (var t = 0; t < 4; t++)
                Assert.IsTrue(InteriorWearPlan.Shards(t, true) * 3 <= limits[t] / 2);
            Assert.IsTrue(InteriorWearPlan.Shards(0, false) >= 1);
            Assert.IsTrue(InteriorWearPlan.ShardNearWindow(0.1));
            Assert.IsFalse(InteriorWearPlan.ShardNearWindow(0.9));
        }
    }
}
