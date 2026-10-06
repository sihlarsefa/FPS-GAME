using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class InteriorDoorTests
    {
        [Test]
        public void OpenSign_SwingsAwayFromActor()
        {
            // Menteşe solda (kanat +X): +açı ucu -Z'ye taşır. Oyuncu +Z'de ise +1 olmalı.
            Assert.AreEqual(1f, WoodenDoorMath.OpenSign(1f, false));
            Assert.AreEqual(-1f, WoodenDoorMath.OpenSign(-1f, false));
            Assert.AreEqual(-1f, WoodenDoorMath.OpenSign(1f, true));
            Assert.AreEqual(1f, WoodenDoorMath.OpenSign(-1f, true));
        }

        [Test]
        public void Step_ConvergesWithoutOvershoot()
        {
            float a = 0f, v = 0f;
            for (var i = 0; i < 200; i++)
            {
                a = WoodenDoorMath.Step(a, 98f, ref v, 0.22f, 0.016f);
                Assert.LessOrEqual(a, 98.0001f);
            }

            Assert.AreEqual(98f, a, 0.1f);
        }

        [Test]
        public void Targets_AndSlamThreshold()
        {
            Assert.AreEqual(0f, WoodenDoorMath.TargetAngle(DoorState.Closed, 1f));
            Assert.AreEqual(-WoodenDoorMath.AjarAngle, WoodenDoorMath.TargetAngle(DoorState.Ajar, -1f));
            Assert.IsFalse(WoodenDoorMath.IsSlam(3f));
            Assert.IsTrue(WoodenDoorMath.IsSlam(6f));
            Assert.AreEqual(-0.55f, WoodenDoorMath.HingeX(1.1f, false), 1e-4f);
            Assert.AreEqual(0.55f, WoodenDoorMath.LeafOffsetX(1.1f, false), 1e-4f);
        }

        [Test]
        public void Plan_IsDeterministic_NoOverlap_KeepsDoorClear()
        {
            var room = new Rect(0f, 0f, 5f, 4f);
            var door = new List<Rect> { new Rect(2f, 0f, 1f, 0.2f) };
            var a = new List<FurniturePlacement>();
            var b = new List<FurniturePlacement>();
            InteriorLayout.Plan(room, RoomKind.Kitchen, new System.Random(7), door, a);
            InteriorLayout.Plan(room, RoomKind.Kitchen, new System.Random(7), door, b);
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 1);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Footprint, b[i].Footprint);
                Assert.IsTrue(room.Contains(a[i].Footprint.min) && room.Contains(a[i].Footprint.max - new Vector2(0.001f, 0.001f)));
                if (!a[i].Blocks)
                    continue;
                Assert.IsFalse(a[i].Footprint.Overlaps(new Rect(1.5f, 0f, 2f, 0.9f)), "kapı önü açık olmalı");
                for (var j = i + 1; j < a.Count; j++)
                    if (a[j].Blocks)
                        Assert.IsFalse(a[i].Footprint.Overlaps(a[j].Footprint));
            }
        }

        [Test]
        public void Classify_SmallRoomIsStorage()
        {
            Assert.AreEqual(RoomKind.Storage, InteriorLayout.Classify(2f, 2.5f, 0, 0));
        }

        [Test]
        public void Sun_ThroughWindow_And_FloorProjection()
        {
            var sun = new Vector3(0f, -0.7f, 0.7f).normalized;
            Assert.IsTrue(InteriorLayout.SunThroughWindow(sun, Vector3.forward));
            Assert.IsFalse(InteriorLayout.SunThroughWindow(sun, Vector3.back));
            Assert.IsFalse(InteriorLayout.SunThroughWindow(new Vector3(0f, 0.5f, 0.5f), Vector3.forward));
            Assert.IsTrue(InteriorLayout.ProjectToFloor(new Vector3(0f, 2f, 0f), sun, 0f, out var hit));
            Assert.AreEqual(0f, hit.y, 1e-4f);
            Assert.AreEqual(2f, hit.z, 1e-3f);
        }

        [Test]
        public void Anchors_RegisterAndSnapshot()
        {
            InteriorAnchors.Clear();
            InteriorAnchors.Register(Vector3.one, InteriorAnchorKind.Table, RoomKind.Kitchen, 0);
            var l = new List<InteriorAnchor>();
            InteriorAnchors.Snapshot(l);
            Assert.AreEqual(1, l.Count);
            InteriorAnchors.Clear();
        }
    }
}
