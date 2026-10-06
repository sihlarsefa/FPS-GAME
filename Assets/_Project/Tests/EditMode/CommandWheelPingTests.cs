#if UNITY_EDITOR
using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CommandWheelPingTests
    {
        [Test]
        public void SliceAt_DeadZoneIsNone()
        {
            Assert.AreEqual(-1, CommandWheelMath.SliceAt(new Vector2(0.1f, 0.1f), 6, 0.28f));
        }

        [Test]
        public void SliceAt_CardinalDirections()
        {
            Assert.AreEqual(0, CommandWheelMath.SliceAt(new Vector2(0f, 1f), 6, 0.28f));
            Assert.AreEqual(1, CommandWheelMath.SliceAt(CommandWheelMath.SliceDirection(1, 6), 6, 0.28f));
            Assert.AreEqual(3, CommandWheelMath.SliceAt(new Vector2(0f, -1f), 6, 0.28f));
            Assert.AreEqual(5, CommandWheelMath.SliceAt(CommandWheelMath.SliceDirection(5, 6), 6, 0.28f));
        }

        [Test]
        public void PingBoard_PlaceReplacesAndExpires()
        {
            PingBoard.Reset();
            PingBoard.Place(1, Vector3.zero, false, 10f);
            PingBoard.Place(1, Vector3.one, true, 11f);
            Assert.AreEqual(1, PingBoard.All.Count);
            Assert.IsTrue(PingBoard.TryGet(1, 18f, out var ping));
            Assert.IsTrue(ping.IsEnemy);
            Assert.AreEqual("Düşman!", ping.Label);
            Assert.IsFalse(PingBoard.TryGet(1, 19.1f, out _));
            Assert.IsFalse(PingBoard.TryGet(2, 12f, out _));
            PingBoard.Reset();
        }

        [Test]
        public void IsEnemyTarget_Rules()
        {
            Assert.IsTrue(PingController.IsEnemyTarget(true, true, 2, 1));
            Assert.IsFalse(PingController.IsEnemyTarget(true, true, 1, 1));
            Assert.IsFalse(PingController.IsEnemyTarget(true, false, 2, 1));
            Assert.IsFalse(PingController.IsEnemyTarget(false, true, 2, 1));
        }
    }
}
#endif
